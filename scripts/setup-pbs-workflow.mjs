#!/usr/bin/env node

import fs from "node:fs";
import path from "node:path";
import process from "node:process";

function loadEnvFile(filePath) {
  if (!fs.existsSync(filePath)) return;
  const content = fs.readFileSync(filePath, "utf-8");
  for (const line of content.split("\n")) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith("#")) continue;
    const eqIdx = trimmed.indexOf("=");
    if (eqIdx <= 0) continue;
    const key = trimmed.slice(0, eqIdx).trim();
    let val = trimmed.slice(eqIdx + 1).trim();
    if ((val.startsWith('"') && val.endsWith('"')) || (val.startsWith("'") && val.endsWith("'"))) {
      val = val.slice(1, -1);
    }
    if (process.env[key] === undefined) {
      process.env[key] = val;
    }
  }
}

const rootDir = path.resolve(path.dirname(new URL(import.meta.url).pathname), "..");
loadEnvFile(path.join(rootDir, ".env.functional"));
loadEnvFile(path.join(rootDir, ".env"));

const CONFIG = {
  swUrl: process.env.FUNCTIONAL_SW_URL || "http://localhost:8101",
  swUser: process.env.FUNCTIONAL_SW_USERNAME || "admin",
  swPassword: process.env.FUNCTIONAL_SW_PASSWORD || "changeme",
};

let resolvedSwUrl = null;

async function resolveWorkingSwUrl() {
  if (resolvedSwUrl) return resolvedSwUrl;

  const candidates = [
    CONFIG.swUrl,
    "http://api:8080",
    "http://localhost:8101",
    "http://127.0.0.1:8101",
  ];

  for (const candidate of candidates) {
    if (!candidate) continue;
    try {
      const res = await fetch(`${candidate}/api/auth/login`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ emailOrUsername: "probe", password: "probe" }),
        signal: AbortSignal.timeout(1500),
      }).catch(() => null);

      if (res && res.status !== 404) {
        resolvedSwUrl = candidate;
        return candidate;
      }
    } catch {}
  }

  resolvedSwUrl = CONFIG.swUrl;
  return resolvedSwUrl;
}

async function apiRequest(endpoint, options = {}, token = null) {
  const baseUrl = await resolveWorkingSwUrl();
  const url = `${baseUrl}${endpoint.startsWith("/") ? "" : "/"}${endpoint}`;
  const headers = {
    "Content-Type": "application/json",
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...(options.headers || {}),
  };

  const res = await fetch(url, { ...options, headers });
  let body = null;
  const contentType = res.headers.get("content-type");
  if (contentType && contentType.includes("application/json")) {
    body = await res.json().catch(() => null);
  } else {
    body = await res.text().catch(() => null);
  }

  return { ok: res.ok, status: res.status, body };
}

async function login() {
  const res = await apiRequest("/api/auth/login", {
    method: "POST",
    body: JSON.stringify({
      emailOrUsername: CONFIG.swUser,
      password: CONFIG.swPassword,
    }),
  });

  if (!res.ok || !res.body?.accessToken) {
    throw new Error(`Login failed (${res.status}): ${JSON.stringify(res.body)}`);
  }

  return res.body.accessToken;
}

async function run() {
  console.log("===============================================================================");
  console.log("PBS Space Time Workflow Setup & Stress Test");
  console.log("===============================================================================");

  const token = await login();
  console.log("✓ Logged in to SocialWorker API");

  // 1. Get Accounts & Bluesky ID
  const accountsRes = await apiRequest("/api/accounts", {}, token);
  const accounts = accountsRes.ok && Array.isArray(accountsRes.body) ? accountsRes.body : [];
  const bskyAccount = accounts.find((a) => a.platform?.toLowerCase() === "bluesky");

  if (bskyAccount) {
    console.log(`✓ Using Bluesky Account: @${bskyAccount.handle} (ID: ${bskyAccount.id})`);
  } else {
    console.warn("⚠ No Bluesky account found. Auto-publishing will not link to an account.");
  }

  // 2. Discover PBS Space Time Feed
  console.log("\n--- Discovering PBS Space Time Channel Feed ---");
  const channelUrl = "https://www.youtube.com/@pbsspacetime";
  let feedUrl = "https://www.youtube.com/feeds/videos.xml?channel_id=UC7_gcs09iThXybpVgjHZ_7g";
  let feedTitle = "PBS Space Time";

  const discRes = await apiRequest(`/api/feeds/discover?url=${encodeURIComponent(channelUrl)}`, {}, token);
  if (discRes.ok && discRes.body?.success && discRes.body?.feedUrl) {
    feedUrl = discRes.body.feedUrl;
    feedTitle = discRes.body.title || feedTitle;
    console.log(`✓ Discovered feed: "${feedTitle}" -> ${feedUrl}`);
  } else {
    console.log(`→ Using direct feed URL: "${feedTitle}" -> ${feedUrl}`);
  }

  // 3. Register Feed Subscription
  const existingFeedsRes = await apiRequest("/api/feeds", {}, token);
  const existingFeeds = existingFeedsRes.ok && Array.isArray(existingFeedsRes.body) ? existingFeedsRes.body : [];

  let feedObj = existingFeeds.find((f) => f.feedUrl === feedUrl);
  if (!feedObj) {
    const createRes = await apiRequest(
      "/api/feeds",
      {
        method: "POST",
        body: JSON.stringify({
          title: feedTitle,
          feedUrl: feedUrl,
          websiteUrl: channelUrl,
        }),
      },
      token
    );
    if (createRes.ok && createRes.body?.id) {
      feedObj = createRes.body;
      console.log(`✓ Added feed subscription: "${feedObj.title}" (ID: ${feedObj.id})`);
    } else {
      throw new Error(`Failed to add feed: ${JSON.stringify(createRes.body)}`);
    }
  } else {
    console.log(`✓ Using existing feed subscription: "${feedObj.title}" (ID: ${feedObj.id})`);
  }

  // 4. Create/Update PBS Space Time Automation Workflow
  const WORKFLOW_TITLE = "PBS Space Time — Astrophysics & Citations";
  const INSTRUCTION_PROMPT =
    "You are an astrophysicist and science communicator writing a deep-dive educational thread based on this PBS Space Time episode. " +
    "Analyze the video summary and transcription carefully. " +
    "Draft a high-impact 3-post Bluesky thread: " +
    "Post 1: An exciting hook summarizing the cosmic mystery or physics paradox discussed, including the YouTube video link (![PBS Space Time](<video_url>)). " +
    "Post 2: Explain the core physics mechanism or theory. Use the search tool to find the primary scientific paper, arXiv preprint, or NASA/ESA study referenced in the episode, and cite it as [Paper Title](<url>). " +
    "Post 3: The philosophical or cosmological conclusion, ending with relevant hashtags (#SpaceTime #Astrophysics #Physics #Cosmology). " +
    "Enforce strict 280-character limit per post. Separate posts with --- on its own line.";

  const workflowsRes = await apiRequest("/api/workflows", {}, token);
  const existingWorkflows = workflowsRes.ok && Array.isArray(workflowsRes.body) ? workflowsRes.body : [];

  let workflowObj = existingWorkflows.find((w) => w.title === WORKFLOW_TITLE);

  const destinationPayload = [
    {
      platform: "Bluesky",
      autoPublish: true,
      accountId: bskyAccount?.id || null,
    },
  ];

  if (!workflowObj) {
    const createWfRes = await apiRequest(
      "/api/workflows",
      {
        method: "POST",
        body: JSON.stringify({
          title: WORKFLOW_TITLE,
          instructionPrompt: INSTRUCTION_PROMPT,
          feedSubscriptionIds: [feedObj.id],
          destinations: destinationPayload,
        }),
      },
      token
    );

    if (createWfRes.ok && createWfRes.body?.id) {
      workflowObj = createWfRes.body;
      console.log(`\n✓ Created Automation Workflow: "${WORKFLOW_TITLE}" (ID: ${workflowObj.id})`);
    } else {
      throw new Error(`Failed to create workflow: ${JSON.stringify(createWfRes.body)}`);
    }
  } else {
    const updateWfRes = await apiRequest(
      `/api/workflows/${workflowObj.id}`,
      {
        method: "PUT",
        body: JSON.stringify({
          title: WORKFLOW_TITLE,
          instructionPrompt: INSTRUCTION_PROMPT,
          feedSubscriptionIds: [feedObj.id],
          destinations: destinationPayload,
        }),
      },
      token
    );

    if (updateWfRes.ok) {
      workflowObj = updateWfRes.body;
      console.log(`\n✓ Updated Workflow: "${WORKFLOW_TITLE}" (ID: ${workflowObj.id})`);
    } else {
      throw new Error(`Failed to update workflow: ${JSON.stringify(updateWfRes.body)}`);
    }
  }

  // 5. Trigger Polling
  console.log("\n--- Triggering Workflow Polling ---");
  const triggerRes = await apiRequest(`/api/workflows/${workflowObj.id}/trigger`, { method: "POST" }, token);
  console.log(`  Trigger response: ${triggerRes.status} (${triggerRes.body?.message || "Triggered successfully"})`);

  console.log("\nWaiting 25 seconds for background workers to ingest latest PBS Space Time episode...");
  await new Promise((r) => setTimeout(r, 25000));

  // 6. Inspect Workflow Drafts
  const draftsRes = await apiRequest(`/api/workflows/${workflowObj.id}/drafts`, {}, token);
  const drafts = draftsRes.ok && Array.isArray(draftsRes.body) ? draftsRes.body : [];

  console.log("\n===============================================================================");
  console.log(`Generated Drafts for "${WORKFLOW_TITLE}" (${drafts.length} total)`);
  console.log("===============================================================================");

  for (const d of drafts.slice(0, 3)) {
    console.log(`\n• Draft ID: ${d.id}`);
    console.log(`  Title: "${d.title}"`);
    console.log(`  Stage: ${d.stage} | Status: ${d.status}`);
    if (d.content) {
      console.log(`  Content Preview:\n${d.content.slice(0, 300)}...`);
    }
  }
}

run().catch((err) => {
  console.error("\n❌ Error:", err.message);
  process.exit(1);
});
