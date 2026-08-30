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
// Main Workflow Setup & Verification
// ---------------------------------------------------------------------------
async function main() {
  console.log("===============================================================================");
  console.log("Social Worker: Feed Automation & Workflow Intelligence Setup");
  console.log("===============================================================================");

  const token = await loginSocialWorker();
  console.log(`✓ Authenticated as ${CONFIG.swUser}\n`);

  // 1. Define Broad Selection of RSS Feeds
  const FEEDS_TO_CREATE = [
    {
      title: "Simon Willison Weblog (AI & Software)",
      feedUrl: "https://simonwillison.net/atom/entries/",
      websiteUrl: "https://simonwillison.net",
    },
    {
      title: "NASA Breaking News (Space & Astronomy)",
      feedUrl: "https://www.nasa.gov/news-release/feed/",
      websiteUrl: "https://www.nasa.gov",
    },
    {
      title: "BBC News Technology (Global Industry)",
      feedUrl: "https://feeds.bbci.co.uk/news/technology/rss.xml",
      websiteUrl: "https://www.bbc.com/news/technology",
    },
    {
      title: "Ars Technica (Tech & Science)",
      feedUrl: "https://feeds.arstechnica.com/arstechnica/index",
      websiteUrl: "https://arstechnica.com",
    },
  ];

  // Fetch existing feeds
  const existingFeedsRes = await apiRequest("/api/feeds", {}, token);
  const existingFeeds = existingFeedsRes.ok && Array.isArray(existingFeedsRes.body) ? existingFeedsRes.body : [];

  const createdFeedMap = new Map();

  for (const feed of FEEDS_TO_CREATE) {
    let feedObj = existingFeeds.find((f) => f.feedUrl === feed.feedUrl);
    if (!feedObj) {
      const createRes = await apiRequest(
        "/api/feeds",
        {
          method: "POST",
          body: JSON.stringify(feed),
        },
        token
      );
      if (createRes.ok && createRes.body?.id) {
        feedObj = createRes.body;
        console.log(`✓ Created Feed Subscription: "${feed.title}" (ID: ${feedObj.id})`);
      } else {
        console.warn(`⚠ Failed to create feed "${feed.title}": ${JSON.stringify(createRes.body)}`);
      }
    } else {
      console.log(`✓ Using existing Feed Subscription: "${feedObj.title}" (ID: ${feedObj.id})`);
    }

    if (feedObj) {
      createdFeedMap.set(feed.title, feedObj.id);
    }
  }

  // 2. Define Workflows with Specific Processing Instructions
  const WORKFLOWS_TO_CREATE = [
    {
      title: "AI & Open Source Engineering Digest",
      instructionPrompt:
        "Focus on practical developer takeaways. Draft a punchy 2-post Bluesky thread: " +
        "Post 1 gives the main development/announcement and why it matters; " +
        "Post 2 highlights technical implementation details and implications for software engineers. " +
        "Strict character limit per post: 280 chars. Separate segments with --- on its own line.",
      feedKeys: ["Simon Willison Weblog (AI & Software)"],
      destinations: [{ platform: "Bluesky", autoPublish: false }],
    },
    {
      title: "Space Exploration & Discovery Dispatch",
      instructionPrompt:
        "Focus on wonder, scientific significance, and human ambition. " +
        "Explain what was discovered or achieved, why it matters for science and astronomy, and what the next milestones are. " +
        "Write as an inspiring 2-segment thread with engaging, accessible language for science enthusiasts. " +
        "Separate segments with --- on its own line.",
      feedKeys: ["NASA Breaking News (Space & Astronomy)"],
      destinations: [{ platform: "Bluesky", autoPublish: false }],
    },
    {
      title: "Global Tech Trends & Market Signals",
      instructionPrompt:
        "Analyze the broader economic, regulatory, and societal impact. " +
        "Highlight who the major players are, what challenges or risks remain, and what long-term trend this signals. " +
        "Format as a concise 2-post thread separated by --- and cite the article source link in the second post.",
      feedKeys: ["BBC News Technology (Global Industry)", "Ars Technica (Tech & Science)"],
      destinations: [{ platform: "Bluesky", autoPublish: false }],
    },
  ];

  // Fetch existing workflows
  const existingWfRes = await apiRequest("/api/workflows", {}, token);
  const existingWorkflows = existingWfRes.ok && Array.isArray(existingWfRes.body) ? existingWfRes.body : [];

  const createdWorkflowIds = [];

  console.log("\n--- Setting up Automation Workflows ---");
  for (const wf of WORKFLOWS_TO_CREATE) {
    const feedSubIds = wf.feedKeys
      .map((key) => createdFeedMap.get(key))
      .filter((id) => Boolean(id));

    let wfObj = existingWorkflows.find((w) => w.title === wf.title);
    if (!wfObj) {
      const createWfRes = await apiRequest(
        "/api/workflows",
        {
          method: "POST",
          body: JSON.stringify({
            title: wf.title,
            instructionPrompt: wf.instructionPrompt,
            feedSubscriptionIds: feedSubIds,
            destinations: wf.destinations,
          }),
        },
        token
      );
      if (createWfRes.ok && createWfRes.body?.id) {
        wfObj = createWfRes.body;
        console.log(`✓ Created Workflow: "${wf.title}" (ID: ${wfObj.id}) with ${feedSubIds.length} feeds`);
      } else {
        console.warn(`⚠ Failed to create workflow "${wf.title}": ${JSON.stringify(createWfRes.body)}`);
      }
    } else {
      console.log(`✓ Using existing Workflow: "${wfObj.title}" (ID: ${wfObj.id})`);
    }

    if (wfObj) {
      createdWorkflowIds.push({ id: wfObj.id, title: wf.title });
    }
  }

  // 3. Trigger Polling and Workflow Execution
  console.log("\n--- Triggering Live Polling & Feed Processing ---");
  for (const wf of createdWorkflowIds) {
    console.log(`→ Triggering polling for workflow "${wf.title}" (ID: ${wf.id})...`);
    const triggerRes = await apiRequest(`/api/workflows/${wf.id}/trigger`, { method: "POST" }, token);
    console.log(`  Trigger status: ${triggerRes.status} (${triggerRes.body?.message || "Triggered"})`);
  }

  // 4. Poll & Verify Output Generation
  console.log("\n--- Waiting for Background Tasks & Verifying Workflow Drafts ---");
  console.log("Waiting 15 seconds for feed fetching, extraction, and LLM thread generation...");
  await new Promise((r) => setTimeout(r, 15000));

  for (const wf of createdWorkflowIds) {
    console.log(`\n===============================================================================`);
    console.log(`Workflow: "${wf.title}"`);
    console.log(`===============================================================================`);

    const draftsRes = await apiRequest(`/api/workflows/${wf.id}/drafts`, {}, token);
    const drafts = draftsRes.ok && Array.isArray(draftsRes.body) ? draftsRes.body : [];

    console.log(`Drafts generated for this workflow: ${drafts.length}`);

    if (drafts.length === 0) {
      console.log("  (Ingestion tasks queued; will appear as background processor completes items)");
    } else {
      for (const d of drafts.slice(0, 3)) {
        console.log(`\n  • Draft ID: ${d.id}`);
        console.log(`    Title: "${d.title}"`);
        console.log(`    Status: ${d.status}`);

        // Fetch full draft content
        const fullDraftRes = await apiRequest(`/api/drafts/${d.id}`, {}, token);
        if (fullDraftRes.ok && fullDraftRes.body?.content) {
          console.log(`    Content Excerpt:\n    ${fullDraftRes.body.content.split("\n").slice(0, 4).join("\n    ")}...`);
        }
      }
    }
  }

  console.log("\n===============================================================================");
  console.log("SUMMARY: WORKFLOWS & FEEDS ARE LIVE");
  console.log("===============================================================================");
  console.log("You can view and manage all workflows at: http://localhost:8100/workflows");
  console.log("You can inspect all generated drafts at:  http://localhost:8100/drafts");
}

main().catch((err) => {
  console.error("Fatal error:", err);
  process.exit(1);
});
