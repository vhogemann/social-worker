#!/usr/bin/env node

import fs from "node:fs";
import path from "node:path";
import process from "node:process";

// ---------------------------------------------------------------------------
// Environment & Configuration Loader
// ---------------------------------------------------------------------------
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
    "http://localhost:8080",
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

      if (res && (res.status === 401 || res.status === 400 || res.status === 200)) {
        resolvedSwUrl = candidate;
        return resolvedSwUrl;
      }
    } catch (_) {}
  }

  resolvedSwUrl = CONFIG.swUrl;
  return resolvedSwUrl;
}

async function apiRequest(endpoint, options = {}, token = null) {
  const baseUrl = await resolveWorkingSwUrl();
  const url = `${baseUrl}${endpoint.startsWith("/") ? endpoint : `/${endpoint}`}`;
  const headers = {
    "Content-Type": "application/json",
    ...(options.headers || {}),
  };
  if (token) {
    headers["Authorization"] = `Bearer ${token}`;
  }

  const response = await fetch(url, {
    ...options,
    headers,
  });

  const contentType = response.headers.get("content-type") || "";
  let body = null;
  if (contentType.includes("application/json")) {
    body = await response.json().catch(() => null);
  } else {
    body = await response.text().catch(() => null);
  }

  return {
    status: response.status,
    ok: response.ok,
    headers: response.headers,
    body,
  };
}

async function loginSocialWorker() {
  const res = await apiRequest("/api/auth/login", {
    method: "POST",
    body: JSON.stringify({
      emailOrUsername: CONFIG.swUser,
      password: CONFIG.swPassword,
    }),
  });

  if (!res.ok || !res.body?.accessToken) {
    throw new Error(`Social Worker login failed (${res.status}): ${JSON.stringify(res.body)}`);
  }

  return res.body.accessToken;
}

// ---------------------------------------------------------------------------
// All Linus Media Group YouTube Channels
// ---------------------------------------------------------------------------
const LMG_CHANNELS = [
  {
    title: "Linus Tech Tips",
    handle: "@LinusTechTips",
    feedUrl: "https://www.youtube.com/feeds/videos.xml?channel_id=UCXuqSBlHAE6Xw-yeJA0Tunw",
    websiteUrl: "https://www.youtube.com/@LinusTechTips",
  },
  {
    title: "TechLinked",
    handle: "@TechLinked",
    feedUrl: "https://www.youtube.com/feeds/videos.xml?channel_id=UCeeFfhMcJa1kjtfZAGskOCA",
    websiteUrl: "https://www.youtube.com/@TechLinked",
  },
  {
    title: "Techquickie",
    handle: "@Techquickie",
    feedUrl: "https://www.youtube.com/feeds/videos.xml?channel_id=UC0vBXGSyV14uvJ4hECDOl0Q",
    websiteUrl: "https://www.youtube.com/@Techquickie",
  },
  {
    title: "ShortCircuit",
    handle: "@ShortCircuit",
    feedUrl: "https://www.youtube.com/feeds/videos.xml?channel_id=UCdBK94H6oZT2Q7l0-b0xmMg",
    websiteUrl: "https://www.youtube.com/@ShortCircuit",
  },
  {
    title: "GameLinked",
    handle: "@GameLinked",
    feedUrl: "https://www.youtube.com/feeds/videos.xml?channel_id=UCHDxYLv8iovIbhrfl16CNyg",
    websiteUrl: "https://www.youtube.com/@GameLinked",
  },
  {
    title: "Mac Address",
    handle: "@MacAddress",
    feedUrl: "https://www.youtube.com/feeds/videos.xml?channel_id=UC0KfjyvabuE2J-RBC6ko2Lw",
    websiteUrl: "https://www.youtube.com/@MacAddress",
  },
  {
    title: "Channel Super Fun",
    handle: "@ChannelSuperFun",
    feedUrl: "https://www.youtube.com/feeds/videos.xml?channel_id=UCBZiUUYeLfS5rIj4TQvgSvA",
    websiteUrl: "https://www.youtube.com/@ChannelSuperFun",
  },
  {
    title: "They're Just Movies",
    handle: "@TheyreJustMovies",
    feedUrl: "https://www.youtube.com/feeds/videos.xml?channel_id=UCt-oJR5teQIjOAxCmIQvcgA",
    websiteUrl: "https://www.youtube.com/@TheyreJustMovies",
  },
];

async function main() {
  console.log("===============================================================================");
  console.log("Setting up Linus Media Group (LMG) YouTube Auto-Publish Workflow");
  console.log("===============================================================================");

  const token = await loginSocialWorker();
  console.log(`✓ Authenticated as ${CONFIG.swUser}\n`);

  // 1. Fetch connected accounts (to associate Bluesky account)
  const accountsRes = await apiRequest("/api/accounts", {}, token);
  const accounts = accountsRes.ok && Array.isArray(accountsRes.body) ? accountsRes.body : [];
  const bskyAccount = accounts.find((a) => a.platform.toLowerCase() === "bluesky");

  if (bskyAccount) {
    console.log(`✓ Found Bluesky account: @${bskyAccount.handle} (ID: ${bskyAccount.id})`);
  } else {
    console.log("ℹ No specific Bluesky account ID found; workflow will use default active Bluesky connection.");
  }

  // 2. Fetch existing feeds
  const feedsRes = await apiRequest("/api/feeds", {}, token);
  const existingFeeds = feedsRes.ok && Array.isArray(feedsRes.body) ? feedsRes.body : [];

  const lmgSubscriptionIds = [];

  console.log("\n--- Registering LMG YouTube Channel Feeds ---");
  for (const ch of LMG_CHANNELS) {
    let feedObj = existingFeeds.find((f) => f.feedUrl === ch.feedUrl);
    if (!feedObj) {
      const createRes = await apiRequest(
        "/api/feeds",
        {
          method: "POST",
          body: JSON.stringify({
            title: `LMG: ${ch.title}`,
            feedUrl: ch.feedUrl,
            websiteUrl: ch.websiteUrl,
          }),
        },
        token
      );
      if (createRes.ok && createRes.body?.id) {
        feedObj = createRes.body;
        console.log(`✓ Added feed: "LMG: ${ch.title}" (ID: ${feedObj.id})`);
      } else {
        console.warn(`⚠ Failed to add feed "${ch.title}": ${JSON.stringify(createRes.body)}`);
      }
    } else {
      console.log(`✓ Using existing feed: "${feedObj.title}" (ID: ${feedObj.id})`);
    }

    if (feedObj) {
      lmgSubscriptionIds.push(feedObj.id);
    }
  }

  console.log(`\nTotal LMG Channel Feeds Configured: ${lmgSubscriptionIds.length}/${LMG_CHANNELS.length}`);

  // 3. Create or Update the LMG Automation Workflow
  const WORKFLOW_TITLE = "Linus Media Group — Auto Bluesky Publisher";
  const INSTRUCTION_PROMPT =
    "You are a tech enthusiast covering Linus Media Group (Linus Tech Tips, TechLinked, Techquickie, ShortCircuit, GameLinked, Mac Address). " +
    "Extract the core product, benchmark, or news story from the video. " +
    "Draft a high-impact 2-post Bluesky thread: " +
    "Post 1: An engaging hook summarizing what Linus/the team tested or covered, including the YouTube video link (![Title](<url>)). " +
    "Post 2: 2-3 key takeaways, performance findings, or verdict, ending with a question for tech enthusiasts. " +
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
          feedSubscriptionIds: lmgSubscriptionIds,
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
          feedSubscriptionIds: lmgSubscriptionIds,
          destinations: destinationPayload,
        }),
      },
      token
    );

    if (updateWfRes.ok) {
      workflowObj = updateWfRes.body;
      console.log(`\n✓ Updated Workflow: "${WORKFLOW_TITLE}" (ID: ${workflowObj.id}) with ${lmgSubscriptionIds.length} feeds`);
    } else {
      throw new Error(`Failed to update workflow: ${JSON.stringify(updateWfRes.body)}`);
    }
  }

  // 4. Trigger Polling on the Workflow
  console.log("\n--- Triggering Workflow Polling ---");
  console.log(`→ Polling all ${lmgSubscriptionIds.length} LMG YouTube channels...`);
  const triggerRes = await apiRequest(`/api/workflows/${workflowObj.id}/trigger`, { method: "POST" }, token);
  console.log(`  Trigger response: ${triggerRes.status} (${triggerRes.body?.message || "Triggered successfully"})`);

  console.log("\nWaiting 20 seconds for background workers to ingest videos and auto-publish to Bluesky...");
  await new Promise((r) => setTimeout(r, 20000));

  // 5. Inspect Generated Workflow Drafts & Publishing Status
  console.log("\n===============================================================================");
  console.log(`Generated & Auto-Published Drafts for "${WORKFLOW_TITLE}"`);
  console.log("===============================================================================");

  const draftsRes = await apiRequest(`/api/workflows/${workflowObj.id}/drafts`, {}, token);
  const drafts = draftsRes.ok && Array.isArray(draftsRes.body) ? draftsRes.body : [];

  console.log(`Total drafts generated: ${drafts.length}`);

  for (const d of drafts.slice(0, 5)) {
    console.log(`\n• Draft: "${d.title}"`);
    console.log(`  Status: ${d.status}`);
    console.log(`  Target Platform: ${d.targetPlatform || "Bluesky"}`);
    if (d.threads && d.threads.length > 0) {
      for (const t of d.threads) {
        console.log(`  - Thread [${t.platform}]: Stage = ${t.stage}`);
      }
    }

    const fullDraftRes = await apiRequest(`/api/drafts/${d.id}`, {}, token);
    if (fullDraftRes.ok && fullDraftRes.body?.content) {
      console.log(`  Content Excerpt:\n    ${fullDraftRes.body.content.split("\n").slice(0, 3).join("\n    ")}...`);
    }
  }

  console.log("\n===============================================================================");
  console.log("LMG YOUTUBE AUTO-PUBLISH WORKFLOW IS ACTIVE");
  console.log("===============================================================================");
  console.log(`Workflow ID: ${workflowObj.id}`);
  console.log(`Manage Workflow in Web UI: http://localhost:8100/workflows`);
  console.log(`View Generated Drafts:      http://localhost:8100/drafts`);
}

main().catch((err) => {
  console.error("Fatal error:", err);
  process.exit(1);
});
