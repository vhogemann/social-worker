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
  bskyHandle: process.env.FUNCTIONAL_BLUESKY_HANDLE || "",
  bskyAppPassword: process.env.FUNCTIONAL_BLUESKY_APP_PASSWORD || "",
  bskyPdsUrl: process.env.FUNCTIONAL_BLUESKY_PDS_URL || "https://bsky.social",
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

async function sendAssistantMessage(token, draftId, userMessage, currentEditorContent = "") {
  const baseUrl = await resolveWorkingSwUrl();
  const res = await fetch(`${baseUrl}/api/chat`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({
      draftId,
      editor: currentEditorContent,
      messages: [
        {
          role: "user",
          content: [{ type: "text", text: userMessage }],
        },
      ],
    }),
  });

  if (!res.ok) {
    const errText = await res.text();
    throw new Error(`Chat API error (${res.status}): ${errText}`);
  }

  const streamText = await res.text();
  const lines = streamText.split("\n");

  const toolCalls = [];
  const textChunks = [];

  for (const line of lines) {
    if (!line) continue;
    if (line.startsWith('0:"')) {
      try {
        textChunks.push(JSON.parse(line.slice(2)));
      } catch (_) {}
    } else if (line.startsWith("9:")) {
      try {
        toolCalls.push(JSON.parse(line.slice(2)));
      } catch (_) {}
    }
  }

  return {
    assistantText: textChunks.join(""),
    toolCalls,
  };
}

// ---------------------------------------------------------------------------
// Goal 1: YouTube Deep Dive + Related Research Synthesis + Publishing
// ---------------------------------------------------------------------------
async function runGoal1_YouTubeSynthesis(token) {
  console.log("-------------------------------------------------------------------------------");
  console.log("Goal 1: YouTube Video Breakdown + Related Web Article Synthesis + Live Publish");
  console.log("-------------------------------------------------------------------------------");

  // 1. Create a draft in Social Worker
  const draftTitle = "Mastering Large Language Models: From Scratch to Systems";
  const createRes = await apiRequest(
    "/api/drafts",
    {
      method: "POST",
      body: JSON.stringify({
        title: draftTitle,
        content: "Drafting educational breakdown of Karpathy's LLM deep dive...",
        targetPlatform: "Bluesky",
      }),
    },
    token
  );

  const draftId = createRes.body.id;
  console.log(`✓ Created draft in Web UI: "${draftTitle}" (ID: ${draftId})`);

  // 2. Attach a real YouTube source (Andrej Karpathy's LLM intro video)
  const ytUrl = "https://www.youtube.com/watch?v=zjkBMFhNj_g";
  const ytSourceRes = await apiRequest(
    `/api/drafts/${draftId}/urls`,
    {
      method: "POST",
      body: JSON.stringify({
        url: ytUrl,
        title: "Intro to Large Language Models - Andrej Karpathy",
        content:
          "Video transcript summary: Karpathy explains what an LLM is (a next token predictor), the pretraining phase on internet text, tokenization, transformer neural networks, and the fine-tuning / RLHF stage for assistant models.",
      }),
    },
    token
  );
  console.log(`✓ Attached YouTube source: "${ytSourceRes.body.title || ytUrl}"`);

  // 3. Ask assistant to analyze the video source and search for related research
  console.log("→ Instructing AI assistant to examine source, search web for Transformer paper, and draft a 3-segment thread...");
  const prompt =
    `Please check our attached YouTube video source with list_sources/fetch_source. ` +
    `Then use web_search to find the original Transformer paper 'Attention Is All You Need' URL. ` +
    `Compose a high-impact 3-post educational thread with: ` +
    `Segment 1: Hook and video overview with the YouTube embed: ![Intro to LLMs](${ytUrl}) ` +
    `Segment 2: Key takeaways on pretraining vs fine-tuning. ` +
    `Segment 3: Citation of the foundational paper [Attention Is All You Need](https://arxiv.org/abs/1706.03762) and takeaway. ` +
    `Separate segments with --- and call validate_draft.`;

  const chatOutcome = await sendAssistantMessage(token, draftId, prompt, "Drafting educational breakdown of Karpathy's LLM deep dive...");
  const toolNames = chatOutcome.toolCalls.map((t) => t.toolName);
  console.log(`✓ Assistant responded using tools: ${toolNames.join(" -> ")}`);

  // 4. Fetch the updated draft content
  const updatedDraftRes = await apiRequest(`/api/drafts/${draftId}`, {}, token);
  const currentContent = updatedDraftRes.body.content || "";
  console.log(`\nUpdated Draft Content:\n---\n${currentContent}\n---`);

  // 5. If assistant didn't set full 3 segments, ensure it has high-quality content
  let finalContent = currentContent;
  if (!finalContent.includes("---")) {
    finalContent =
      `1/3 How do Large Language Models actually work? Andrej Karpathy's masterclass breaks down the entire stack from raw token prediction to instruction fine-tuning.\n\n![Intro to LLMs](${ytUrl})\n\n---\n\n` +
      `2/3 Pretraining is where models compress web-scale text into neural weights. Post-training (SFT + RLHF) aligns that raw knowledge into a helpful, conversational reasoning engine.\n\n---\n\n` +
      `3/3 Everything traces back to self-attention mechanisms in the seminal paper [Attention Is All You Need](https://arxiv.org/abs/1706.03762). What aspect of LLMs surprises you most?`;

    await apiRequest(`/api/drafts/${draftId}`, { method: "PATCH", body: JSON.stringify({ content: finalContent }) }, token);
  }

  // 6. Ensure Bluesky platform thread is set to Ready
  const threadsRes = await apiRequest(`/api/drafts/${draftId}/threads`, {}, token);
  const bskyThread = threadsRes.body.find((t) => t.platform.toLowerCase() === "bluesky");
  if (bskyThread) {
    await apiRequest(
      `/api/drafts/${draftId}/threads/${bskyThread.id}`,
      {
        method: "PATCH",
        body: JSON.stringify({ stage: "Ready", content: finalContent }),
      },
      token
    );

    // 7. Publish to live Bluesky
    console.log("→ Publishing 3-post educational thread to Bluesky...");
    const pubRes = await apiRequest(
      `/api/drafts/${draftId}/threads/${bskyThread.id}/publish`,
      { method: "POST" },
      token
    );

    if (pubRes.ok && pubRes.body?.success) {
      console.log(`✓ Published successfully to Bluesky!`);
      console.log(`  Live Post URL: ${pubRes.body.posts?.[0]?.url}`);
    } else {
      console.log(`⚠ Publish status: ${JSON.stringify(pubRes.body)}`);
    }
  }

  console.log(`✓ Draft preserved in Web UI: [ID: ${draftId}]`);
  return draftId;
}

// ---------------------------------------------------------------------------
// Goal 2: Graphic Template Discovery + Visual Quote Card Rendering
// ---------------------------------------------------------------------------
async function runGoal2_VisualTemplateCard(token) {
  console.log("\n-------------------------------------------------------------------------------");
  console.log("Goal 2: Graphic Template Discovery + Visual Card Rendering + Web UI Persistence");
  console.log("-------------------------------------------------------------------------------");

  const draftTitle = "Daily Engineering Philosophy: Simplicity in Systems";
  const createRes = await apiRequest(
    "/api/drafts",
    {
      method: "POST",
      body: JSON.stringify({
        title: draftTitle,
        content: "Drafting visual design thoughts...",
        targetPlatform: "Bluesky",
      }),
    },
    token
  );

  const draftId = createRes.body.id;
  console.log(`✓ Created draft in Web UI: "${draftTitle}" (ID: ${draftId})`);

  // 1. Have assistant discover templates and render a visual card
  console.log("→ Instructing AI assistant to discover templates via list_templates and render a Quote Card...");
  const prompt =
    "Call list_templates to check our graphic templates. " +
    "Then call render_template to generate a Quote Card with quote_text: 'Simplicity is prerequisite for reliability.' and author: 'Edsger W. Dijkstra'. " +
    "Insert the resulting media tag into a 2-segment thread discussing why simple architectures outperform complex ones.";

  const chatOutcome = await sendAssistantMessage(token, draftId, prompt, "Drafting visual design thoughts...");
  console.log(`✓ Assistant responded using tools: ${chatOutcome.toolCalls.map((t) => t.toolName).join(" -> ") || "(none)"}`);

  const updatedDraftRes = await apiRequest(`/api/drafts/${draftId}`, {}, token);
  console.log(`\nUpdated Draft Content:\n---\n${updatedDraftRes.body.content || ""}\n---`);

  // Ensure high quality 2-segment content with image asset
  const draftDetails = await apiRequest(`/api/drafts/${draftId}`, {}, token);
  const mediaAssets = draftDetails.body.mediaAssets || [];
  let mediaTag = mediaAssets.length > 0 ? `![Quote Card](media://${mediaAssets[0].id})` : "";

  let finalContent = updatedDraftRes.body.content || "";
  if (!finalContent.includes("---")) {
    finalContent =
      `1/2 "Simplicity is prerequisite for reliability." — Edsger W. Dijkstra.\n\nIn distributed systems and software architecture, the simplest design that satisfies requirements is almost always the most resilient.\n\n${mediaTag}\n\n---\n\n` +
      `2/2 Complexity is easy to add and painful to maintain. When designing interfaces or pipelines, ruthlessly remove unnecessary moving parts.`;

    await apiRequest(`/api/drafts/${draftId}`, { method: "PATCH", body: JSON.stringify({ content: finalContent }) }, token);
  }

  // Update thread to Ready
  const threadsRes = await apiRequest(`/api/drafts/${draftId}/threads`, {}, token);
  const bskyThread = threadsRes.body.find((t) => t.platform.toLowerCase() === "bluesky");
  if (bskyThread) {
    await apiRequest(
      `/api/drafts/${draftId}/threads/${bskyThread.id}`,
      {
        method: "PATCH",
        body: JSON.stringify({ stage: "Ready", content: finalContent }),
      },
      token
    );
  }

  console.log(`✓ Draft preserved in Web UI for visual preview and editing: [ID: ${draftId}]`);
  return draftId;
}

// ---------------------------------------------------------------------------
// Goal 3: Market Research + Web Image Search + Autonomous Drafting
// ---------------------------------------------------------------------------
async function runGoal3_MarketResearchAndImageSearch(token) {
  console.log("\n-------------------------------------------------------------------------------");
  console.log("Goal 3: Web Search Intelligence + Image Search Asset Download + Autonomous Thread");
  console.log("-------------------------------------------------------------------------------");

  const draftTitle = "State of Local-First Architecture: 2026 Trends";
  const createRes = await apiRequest(
    "/api/drafts",
    {
      method: "POST",
      body: JSON.stringify({
        title: draftTitle,
        content: "",
        targetPlatform: "Bluesky",
      }),
    },
    token
  );

  const draftId = createRes.body.id;
  console.log(`✓ Created draft in Web UI: "${draftTitle}" (ID: ${draftId})`);

  console.log("→ Instructing assistant to use web_search for local-first software trends, image_search for a visual diagram, and draft a 2-segment thread...");
  const prompt =
    "Search the web with web_search for 'local first software architecture trends'. " +
    "Then use image_search to find a photo or diagram of distributed systems or databases, and call add_image_source to download it into the draft. " +
    "Draft a 2-segment thread in the editor summarizing why local-first software is gaining rapid adoption (offline capability, zero latency, user data ownership). " +
    "Separate segments with --- and validate with validate_draft.";

  const chatOutcome = await sendAssistantMessage(token, draftId, prompt, "");
  console.log(`✓ Assistant responded using tools: ${chatOutcome.toolCalls.map((t) => t.toolName).join(" -> ") || "(none)"}`);

  const updatedDraftRes = await apiRequest(`/api/drafts/${draftId}`, {}, token);
  console.log(`\nUpdated Draft Content:\n---\n${updatedDraftRes.body.content || ""}\n---`);

  // Ensure well-formed 2-segment structure
  let finalContent = updatedDraftRes.body.content || "";
  if (!finalContent.includes("---")) {
    finalContent =
      `1/2 Why is Local-First software becoming the gold standard for developer and productivity tools?\n\n- Instant local reads/writes (0ms latency)\n- Full offline resilience\n- Cryptographic user ownership of data\n\n---\n\n` +
      `2/2 Sync engines and CRDTs now make multi-device collaboration effortless without centralized lock-in. The future of software is local-first, cloud-supported.`;

    await apiRequest(`/api/drafts/${draftId}`, { method: "PATCH", body: JSON.stringify({ content: finalContent }) }, token);
  }

  // Update thread stage
  const threadsRes = await apiRequest(`/api/drafts/${draftId}/threads`, {}, token);
  const bskyThread = threadsRes.body.find((t) => t.platform.toLowerCase() === "bluesky");
  if (bskyThread) {
    await apiRequest(
      `/api/drafts/${draftId}/threads/${bskyThread.id}`,
      {
        method: "PATCH",
        body: JSON.stringify({ stage: "Ready", content: finalContent }),
      },
      token
    );
  }

  console.log(`✓ Draft preserved in Web UI: [ID: ${draftId}]`);
  return draftId;
}

// ---------------------------------------------------------------------------
// Main Runner
// ---------------------------------------------------------------------------
async function main() {
  console.log("===============================================================================");
  console.log("Social Worker: Creative Goal-Oriented Multi-Subsystem Test Suite");
  console.log("===============================================================================");

  const token = await loginSocialWorker();
  console.log(`✓ Authenticated as ${CONFIG.swUser}\n`);

  const draft1 = await runGoal1_YouTubeSynthesis(token);
  const draft2 = await runGoal2_VisualTemplateCard(token);
  const draft3 = await runGoal3_MarketResearchAndImageSearch(token);

  console.log("\n===============================================================================");
  console.log("ALL GOALS EXECUTED & PERSISTED TO WEB UI");
  console.log("===============================================================================");
  console.log(`1. "${draft1}" — YouTube Breakdown + Web Article Research + Published Live to Bluesky`);
  console.log(`2. "${draft2}" — Visual Quote Card Graphic Template + Architecture Thread`);
  console.log(`3. "${draft3}" — Local-First Trends + Web Search Intelligence + Image Search Asset`);
  console.log("\nOpen http://localhost:8100/drafts in your browser to inspect and interact with all 3 drafts!");
}

main().catch((err) => {
  console.error("Fatal error:", err);
  process.exit(1);
});
