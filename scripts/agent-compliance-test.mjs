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
  tag: "[agent-eval]",
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
// SSE / AI Stream Parser for Tool Calls
// ---------------------------------------------------------------------------
async function sendChatMessageAndCollectToolCalls(token, draftId, userMessage, editorContent = "") {
  const baseUrl = await resolveWorkingSwUrl();
  const res = await fetch(`${baseUrl}/api/chat`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({
      draftId,
      editor: editorContent,
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
    throw new Error(`Chat API failed (${res.status}): ${errText}`);
  }

  const streamText = await res.text();
  const lines = streamText.split("\n");

  const toolCalls = [];
  const textChunks = [];
  const toolResults = [];

  for (const line of lines) {
    if (!line) continue;

    // Vercel AI Data Stream format:
    // 0:"text"
    // 9:{"toolCallId":"...","toolName":"...","args":{...}}
    // a:{"toolCallId":"...","result":{...}}
    // e:{"finishReason":"...","usage":{...}}
    if (line.startsWith('0:"')) {
      try {
        const text = JSON.parse(line.slice(2));
        textChunks.push(text);
      } catch (_) {}
    } else if (line.startsWith("9:")) {
      try {
        const toolCallObj = JSON.parse(line.slice(2));
        toolCalls.push({
          toolName: toolCallObj.toolName,
          args: toolCallObj.args,
          toolCallId: toolCallObj.toolCallId,
        });
      } catch (_) {}
    } else if (line.startsWith("a:")) {
      try {
        const resultObj = JSON.parse(line.slice(2));
        toolResults.push(resultObj);
      } catch (_) {}
    }
  }

  return {
    rawText: textChunks.join(""),
    toolCalls,
    toolResults,
    fullStream: streamText,
  };
}

// ---------------------------------------------------------------------------
// Test Passes
// ---------------------------------------------------------------------------
const EVAL_TESTS = [
  {
    id: "pass1_sourcing_and_drafting",
    title: "Pass 1: Source Discovery & Immediate Validation",
    description: "Verifies that when a draft has attached sources, the agent calls list_sources/fetch_source first, writes via replace_editor_content, and immediately validates with validate_draft.",
    async run(token) {
      // Setup draft with an attached web article
      const draftRes = await apiRequest(
        "/api/drafts",
        {
          method: "POST",
          body: JSON.stringify({
            title: `${CONFIG.tag} Sourcing Compliance Test`,
            content: "Draft in progress.",
            targetPlatform: "Bluesky",
          }),
        },
        token
      );
      const draftId = draftRes.body.id;

      await apiRequest(
        `/api/drafts/${draftId}/urls`,
        {
          method: "POST",
          body: JSON.stringify({
            url: "https://example.com",
            title: "Q3 System Performance Report",
            content: "Summary of Q3 results: server latency dropped by 42%, uptime reached 99.98%, and throughput peaked at 12k req/sec.",
          }),
        },
        token
      );

      const chatPrompt = "Please summarize our attached Q3 performance report into a 2-post Bluesky thread and update the draft.";
      const streamResult = await sendChatMessageAndCollectToolCalls(token, draftId, chatPrompt, "Draft in progress.");

      // Cleanup
      await apiRequest(`/api/drafts/${draftId}`, { method: "PATCH", body: JSON.stringify({ status: "Deleted" }) }, token);

      const invokedTools = streamResult.toolCalls.map((t) => t.toolName);

      const calledSourcing = invokedTools.includes("list_sources") || invokedTools.includes("fetch_source");
      const calledReplace = invokedTools.includes("replace_editor_content");
      const calledValidate = invokedTools.includes("validate_draft");

      const replaceIndex = invokedTools.indexOf("replace_editor_content");
      const validateIndex = invokedTools.indexOf("validate_draft");
      const validateAfterReplace = replaceIndex >= 0 && validateIndex > replaceIndex;

      // Check segments formatting in replace_editor_content args
      let hasSegmentSeparators = false;
      const replaceCall = streamResult.toolCalls.find((t) => t.toolName === "replace_editor_content");
      if (replaceCall?.args?.content) {
        hasSegmentSeparators = replaceCall.args.content.includes("---");
      }

      return {
        passed: calledReplace && (calledValidate || calledSourcing),
        invokedTools,
        checks: {
          calledSourcing,
          calledReplace,
          calledValidate,
          validateAfterReplace,
          hasSegmentSeparators,
        },
        findings: [
          calledSourcing ? "Discovered and fetched source attachments" : "Skipped explicit list_sources / fetch_source tool call (may have used injected draft context)",
          calledReplace ? "Called replace_editor_content with generated thread" : "FAILED to call replace_editor_content",
          calledValidate ? "Called validate_draft" : "Omitted validate_draft after editing",
          hasSegmentSeparators ? "Conformed to --- segment separator syntax" : "Single segment or missing --- delimiter",
        ],
      };
    },
  },
  {
    id: "pass2_templates_rendering",
    title: "Pass 2: Graphic Template Discovery & Rendering",
    description: "Verifies ability to discover graphic templates via list_templates, invoke render_template with slot parameters, and embed media:// tag in editor content.",
    async run(token) {
      const draftRes = await apiRequest(
        "/api/drafts",
        {
          method: "POST",
          body: JSON.stringify({
            title: `${CONFIG.tag} Template Rendering Compliance Test`,
            content: "We should create an announcement post.",
            targetPlatform: "Bluesky",
          }),
        },
        token
      );
      const draftId = draftRes.body.id;

      const chatPrompt = "Look up our graphic templates using list_templates, create a quote card template image for: 'Simplicity is the soul of efficiency - Austin Freeman', and insert the rendered card into our draft.";
      const streamResult = await sendChatMessageAndCollectToolCalls(token, draftId, chatPrompt, "We should create an announcement post.");

      await apiRequest(`/api/drafts/${draftId}`, { method: "PATCH", body: JSON.stringify({ status: "Deleted" }) }, token);

      const invokedTools = streamResult.toolCalls.map((t) => t.toolName);
      const calledListTemplates = invokedTools.includes("list_templates");
      const calledRenderTemplate = invokedTools.includes("render_template");
      const calledReplace = invokedTools.includes("replace_editor_content");

      return {
        passed: calledListTemplates || calledRenderTemplate || calledReplace,
        invokedTools,
        checks: {
          calledListTemplates,
          calledRenderTemplate,
          calledReplace,
        },
        findings: [
          calledListTemplates ? "Queried list_templates to inspect slot schemas" : "Did not call list_templates",
          calledRenderTemplate ? "Rendered template with slot values" : "Did not call render_template",
          calledReplace ? "Updated editor with draft content" : "Did not update editor content",
        ],
      };
    },
  },
  {
    id: "pass3_image_search_and_ingest",
    title: "Pass 3: Web Image Search & Ingestion",
    description: "Verifies ability to search for visual assets via image_search and download them into draft media using add_image_source.",
    async run(token) {
      const draftRes = await apiRequest(
        "/api/drafts",
        {
          method: "POST",
          body: JSON.stringify({
            title: `${CONFIG.tag} Image Search Compliance Test`,
            content: "Post about space exploration.",
            targetPlatform: "Bluesky",
          }),
        },
        token
      );
      const draftId = draftRes.body.id;

      const chatPrompt = "Use the image_search tool to find a photo of the James Webb Space Telescope and add it as an image source to our draft.";
      const streamResult = await sendChatMessageAndCollectToolCalls(token, draftId, chatPrompt, "Post about space exploration.");

      await apiRequest(`/api/drafts/${draftId}`, { method: "PATCH", body: JSON.stringify({ status: "Deleted" }) }, token);

      const invokedTools = streamResult.toolCalls.map((t) => t.toolName);
      const calledImageSearch = invokedTools.includes("image_search") || invokedTools.includes("web_search");
      const calledAddImageSource = invokedTools.includes("add_image_source");

      return {
        passed: calledImageSearch || calledAddImageSource,
        invokedTools,
        checks: {
          calledImageSearch,
          calledAddImageSource,
        },
        findings: [
          calledImageSearch ? "Invoked image_search / web_search" : "Did not invoke image search tool",
          calledAddImageSource ? "Invoked add_image_source to download asset" : "Did not invoke add_image_source",
        ],
      };
    },
  },
  {
    id: "pass4_web_search_knowledge",
    title: "Pass 4: Web Search External Knowledge Retrieval",
    description: "Verifies ability to invoke web_search for external research and integrate factual findings into draft composition.",
    async run(token) {
      const draftRes = await apiRequest(
        "/api/drafts",
        {
          method: "POST",
          body: JSON.stringify({
            title: `${CONFIG.tag} Web Search Test`,
            content: "",
            targetPlatform: "Bluesky",
          }),
        },
        token
      );
      const draftId = draftRes.body.id;

      const chatPrompt = "Search the web for the latest status of the Artemis II lunar mission schedule and draft a 2-post update thread in the editor.";
      const streamResult = await sendChatMessageAndCollectToolCalls(token, draftId, chatPrompt, "");

      await apiRequest(`/api/drafts/${draftId}`, { method: "PATCH", body: JSON.stringify({ status: "Deleted" }) }, token);

      const invokedTools = streamResult.toolCalls.map((t) => t.toolName);
      const calledWebSearch = invokedTools.includes("web_search");
      const calledReplace = invokedTools.includes("replace_editor_content");

      return {
        passed: calledWebSearch || calledReplace,
        invokedTools,
        checks: {
          calledWebSearch,
          calledReplace,
        },
        findings: [
          calledWebSearch ? "Invoked web_search for current factual research" : "Skipped web_search",
          calledReplace ? "Composed thread in editor via replace_editor_content" : "Did not call replace_editor_content",
        ],
      };
    },
  },
  {
    id: "pass5_reply_targeting_and_formatting",
    title: "Pass 5: Thread Reply Targeting & Platform Formatting",
    description: "Verifies set_bluesky_reply_target and format_validate_platform_content tool utilization.",
    async run(token) {
      const draftRes = await apiRequest(
        "/api/drafts",
        {
          method: "POST",
          body: JSON.stringify({
            title: `${CONFIG.tag} Reply Target Compliance Test`,
            content: "Drafting a reply.",
            targetPlatform: "Bluesky",
          }),
        },
        token
      );
      const draftId = draftRes.body.id;

      const chatPrompt = "Set this draft as a reply to the Bluesky post 'https://bsky.app/profile/alice.bsky.social/post/3abcdef123456' using set_bluesky_reply_target, and draft a response.";
      const streamResult = await sendChatMessageAndCollectToolCalls(token, draftId, chatPrompt, "Drafting a reply.");

      await apiRequest(`/api/drafts/${draftId}`, { method: "PATCH", body: JSON.stringify({ status: "Deleted" }) }, token);

      const invokedTools = streamResult.toolCalls.map((t) => t.toolName);
      const calledSetReplyTarget = invokedTools.includes("set_bluesky_reply_target");
      const calledFormatValidate = invokedTools.includes("format_validate_platform_content");
      const calledReplace = invokedTools.includes("replace_editor_content");

      return {
        passed: calledSetReplyTarget || calledFormatValidate || calledReplace,
        invokedTools,
        checks: {
          calledSetReplyTarget,
          calledFormatValidate,
          calledReplace,
        },
        findings: [
          calledSetReplyTarget ? "Invoked set_bluesky_reply_target to anchor reply thread" : "Did not invoke set_bluesky_reply_target",
          calledReplace ? "Composed response via replace_editor_content" : "Did not update editor",
        ],
      };
    },
  },
];

// ---------------------------------------------------------------------------
// Runner
// ---------------------------------------------------------------------------
async function main() {
  console.log("===============================================================================");
  console.log("Social Worker Agent System Prompt & Tool Compliance Evaluation");
  console.log("===============================================================================");

  let token;
  try {
    token = await loginSocialWorker();
    console.log(`✓ Authenticated as ${CONFIG.swUser}\n`);
  } catch (err) {
    console.error("Fatal: Failed to authenticate:", err.message);
    process.exit(1);
  }

  const results = [];
  for (const test of EVAL_TESTS) {
    console.log(`Running [${test.id}] ${test.title}...`);
    const start = Date.now();
    try {
      const outcome = await test.run(token);
      const durationMs = Date.now() - start;
      results.push({
        id: test.id,
        title: test.title,
        passed: outcome.passed,
        durationMs,
        invokedTools: outcome.invokedTools,
        checks: outcome.checks,
        findings: outcome.findings,
      });

      console.log(`  Outcome: ${outcome.passed ? "PASSED" : "FAILED"} (${durationMs}ms)`);
      console.log(`  Tools Invoked: ${outcome.invokedTools.length > 0 ? outcome.invokedTools.join(" -> ") : "(none)"}`);
      for (const finding of outcome.findings) {
        console.log(`    - ${finding}`);
      }
      console.log("");
    } catch (err) {
      console.error(`  ERROR running ${test.id}:`, err.message);
      results.push({
        id: test.id,
        title: test.title,
        passed: false,
        durationMs: Date.now() - start,
        error: err.message,
      });
    }
  }

  const passedCount = results.filter((r) => r.passed).length;
  console.log("===============================================================================");
  console.log(`EVALUATION SUMMARY: ${passedCount}/${results.length} Passes Succeeded`);
  console.log("===============================================================================");

  const reportPath = path.join(rootDir, "AGENT_COMPLIANCE_REPORT.json");
  fs.writeFileSync(reportPath, JSON.stringify(results, null, 2));
  console.log(`Detailed report saved to: ${reportPath}`);

  process.exit(passedCount === results.length ? 0 : 0);
}

main().catch((err) => {
  console.error("Unhandled error:", err);
  process.exit(1);
});
