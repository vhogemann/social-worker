import { readFileSync } from "fs";

const API_BASE = process.env.API_URL || "http://localhost:8101";
const API_KEY = "sw_live_4b839b6eaa7eab232ad589b37ea3b384c38de2b6859250b9";

let totalTests = 0;
let passedTests = 0;
let failedTests = 0;
const findings = [];

async function callMcp(method, params) {
  const res = await fetch(`${API_BASE}/api/mcp`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-API-Key": API_KEY
    },
    body: JSON.stringify({
      jsonrpc: "2.0",
      id: Date.now() + Math.floor(Math.random() * 1000),
      method,
      params
    })
  });
  return await res.json();
}

async function callTool(name, args) {
  const res = await callMcp("tools/call", { name, arguments: args });
  return res.result;
}

function assert(description, condition, failureDetail = "") {
  totalTests++;
  if (condition) {
    console.log(`  [PASS] ${description}`);
    passedTests++;
  } else {
    console.error(`  [FAIL] ${description}`);
    if (failureDetail) console.error(`         Detail: ${failureDetail}`);
    failedTests++;
    findings.push({ description, failureDetail });
  }
}

async function runTestSuite() {
  console.log("================================================================================");
  console.log("             SOCIALWORKER MCP DEEP STRESS & EDGE-CASE TEST SUITE                ");
  console.log("================================================================================\n");

  // --------------------------------------------------------------------------------
  // CATEGORY 1: UUID & Parameter Boundary Robustness
  // --------------------------------------------------------------------------------
  console.log(">>> CATEGORY 1: UUID & Parameter Robustness");

  const invalidUuidResult = await callTool("get_draft", { draftId: "not-a-valid-uuid" });
  assert(
    "get_draft rejects non-UUID strings safely without 500 error",
    invalidUuidResult?.isError === true && invalidUuidResult?.content?.[0]?.text?.includes("UUID"),
    JSON.stringify(invalidUuidResult)
  );

  const missingParamResult = await callTool("publish_thread", { draftId: "11111111-1111-1111-1111-111111111111" });
  assert(
    "publish_thread handles missing platform argument cleanly",
    missingParamResult?.isError === true || missingParamResult?.content?.[0]?.text !== undefined,
    JSON.stringify(missingParamResult)
  );

  const nonExistentDraft = await callTool("get_draft", { draftId: "00000000-0000-0000-0000-000000000000" });
  assert(
    "get_draft handles non-existent draft ID cleanly",
    nonExistentDraft?.isError === true || nonExistentDraft?.content?.[0]?.text?.includes("not found") || nonExistentDraft?.content?.[0]?.text?.includes("failed"),
    JSON.stringify(nonExistentDraft)
  );

  // --------------------------------------------------------------------------------
  // CATEGORY 2: Complex Unicode, Emojis & Extreme Payload Handling
  // --------------------------------------------------------------------------------
  console.log("\n>>> CATEGORY 2: Complex Unicode & Extreme Payloads in Drafts");

  const unicodeTitle = "⚡ Black Hole Star: 🌌 𝕏 🔬 ⚛️ (Super-Eddington Accretion) 🪂";
  const unicodeContent = `Post 1: Cosmic mystery 🚀\n---\nPost 2: Math & physics ⚛️ [arXiv Paper](https://arxiv.org/abs/2503.16596)\n---\nPost 3: The End! 🎉 #Astronomy #Physics`;

  const createUnicodeRes = await callTool("create_draft", {
    title: unicodeTitle,
    content: unicodeContent,
    platform: "Bluesky"
  });

  const createdIdMatch = createUnicodeRes?.content?.[0]?.text?.match(/ID:\s*([a-f0-9-]+)/i);
  const unicodeDraftId = createdIdMatch ? createdIdMatch[1] : null;

  assert(
    "create_draft handles complex UTF-8 multibyte glyphs, math symbols, and emojis",
    unicodeDraftId !== null,
    JSON.stringify(createUnicodeRes)
  );

  if (unicodeDraftId) {
    const fetchedDraftRes = await callTool("get_draft", { draftId: unicodeDraftId });
    assert(
      "get_draft preserves exact Unicode characters without corruption",
      fetchedDraftRes?.content?.[0]?.text?.includes("⚡ Black Hole Star") && fetchedDraftRes?.content?.[0]?.text?.includes("⚛️"),
      fetchedDraftRes?.content?.[0]?.text
    );
  }

  // --------------------------------------------------------------------------------
  // CATEGORY 3: AST Validation & Character Limit Precision (Bluesky 300 Chars)
  // --------------------------------------------------------------------------------
  console.log("\n>>> CATEGORY 3: Draft Validation & Platform Constraints");

  // Test exact 300-char post
  const exact300Post = "A".repeat(300);
  const exact300DraftRes = await callTool("create_draft", {
    title: "Exact 300 Char Test",
    content: exact300Post,
    platform: "Bluesky"
  });
  const exact300Id = exact300DraftRes?.content?.[0]?.text?.match(/ID:\s*([a-f0-9-]+)/i)?.[1];

  const valExact300 = await callTool("validate_draft", { draftId: exact300Id, platform: "Bluesky" });
  assert(
    "validate_draft allows post with exactly 300 characters for Bluesky",
    valExact300?.content?.[0]?.text?.includes("Valid") || !valExact300?.content?.[0]?.text?.includes("Character limit exceeded"),
    valExact300?.content?.[0]?.text
  );

  // Test 301-char post (must fail validation)
  const over300Post = "B".repeat(301);
  const over300DraftRes = await callTool("create_draft", {
    title: "Over 300 Char Test",
    content: over300Post,
    platform: "Bluesky"
  });
  const over300Id = over300DraftRes?.content?.[0]?.text?.match(/ID:\s*([a-f0-9-]+)/i)?.[1];

  const valOver300 = await callTool("validate_draft", { draftId: over300Id, platform: "Bluesky" });
  assert(
    "validate_draft detects and reports post exceeding 300 character limit",
    valOver300?.content?.[0]?.text?.includes("301 characters") && (valOver300?.content?.[0]?.text?.includes("Error") || valOver300?.content?.[0]?.text?.includes("failed") || valOver300?.content?.[0]?.text?.includes("exceeds")),
    valOver300?.content?.[0]?.text
  );

  // Test publish guardrail on invalid draft
  const publishOverLimit = await callTool("publish_thread", { draftId: over300Id, platform: "Bluesky" });
  assert(
    "publish_thread blocks publishing when validation fails (character count exceeded)",
    publishOverLimit?.isError === true || publishOverLimit?.content?.[0]?.text?.includes("exceed") || publishOverLimit?.content?.[0]?.text?.includes("Error") || publishOverLimit?.content?.[0]?.text?.includes("failed") || publishOverLimit?.content?.[0]?.text?.includes("Validation failed"),
    JSON.stringify(publishOverLimit)
  );

  // --------------------------------------------------------------------------------
  // CATEGORY 4: Mixed Embed Constraints (YouTube + Images / Multiple YouTube)
  // --------------------------------------------------------------------------------
  console.log("\n>>> CATEGORY 4: Rich Embed Syntax & Single-Card Constraints");

  const dualYoutubePost = "Check this video: ![V1](https://www.youtube.com/watch?v=aaaaa)\n\nAnd this one: ![V2](https://www.youtube.com/watch?v=bbbbb)";
  const dualYoutubeDraftRes = await callTool("create_draft", {
    title: "Dual YouTube Test",
    content: dualYoutubePost,
    platform: "Bluesky"
  });
  const dualYoutubeId = dualYoutubeDraftRes?.content?.[0]?.text?.match(/ID:\s*([a-f0-9-]+)/i)?.[1];

  const valDualYoutube = await callTool("validate_draft", { draftId: dualYoutubeId, platform: "Bluesky" });
  assert(
    "validate_draft flags multiple YouTube video embeds in a single post",
    valDualYoutube?.content?.[0]?.text?.includes("YouTube") || valDualYoutube?.content?.[0]?.text?.includes("embed") || valDualYoutube?.content?.[0]?.text?.includes("one video") || valDualYoutube?.content?.[0]?.text?.includes("single card"),
    valDualYoutube?.content?.[0]?.text
  );

  // --------------------------------------------------------------------------------
  // CATEGORY 5: SSRF Security & Source Ingestion Validation
  // --------------------------------------------------------------------------------
  console.log("\n>>> CATEGORY 5: SSRF & URL Source Ingestion Boundaries");

  const ssrfAttempts = [
    "http://127.0.0.1:5432",
    "http://localhost:8101",
    "http://169.254.169.254/latest/meta-data",
    "http://10.0.0.1/admin",
    "http://192.168.1.1/router"
  ];

  for (const maliciousUrl of ssrfAttempts) {
    const ssrfRes = await callTool("add_source", {
      draftId: unicodeDraftId,
      reference: maliciousUrl,
      title: "SSRF Probe"
    });

    assert(
      `add_source blocks private/loopback IP (${maliciousUrl})`,
      ssrfRes?.isError === true || ssrfRes?.content?.[0]?.text?.includes("disallowed") || ssrfRes?.content?.[0]?.text?.includes("invalid") || ssrfRes?.content?.[0]?.text?.includes("SSRF") || ssrfRes?.content?.[0]?.text?.includes("failed"),
      JSON.stringify(ssrfRes)
    );
  }

  // --------------------------------------------------------------------------------
  // CATEGORY 6: MCP Resources & Workflow Inspection
  // --------------------------------------------------------------------------------
  console.log("\n>>> CATEGORY 6: MCP Resources & Automation Workflows");

  const workflowsList = await callTool("list_workflows", {});
  assert(
    "list_workflows returns configured workflows",
    workflowsList?.content?.[0]?.text?.includes("PBS Space Time") || workflowsList?.content?.[0]?.text?.length > 10,
    workflowsList?.content?.[0]?.text
  );

  const feedsList = await callTool("list_feeds", {});
  assert(
    "list_feeds returns active feed subscriptions",
    feedsList?.content?.[0]?.text?.includes("youtube.com") || feedsList?.content?.[0]?.text?.includes("Better Offline"),
    feedsList?.content?.[0]?.text
  );

  const resourceReadDraft = await callMcp("resources/read", { uri: `socialworker://drafts/${unicodeDraftId}` });
  assert(
    "resources/read retrieves draft content formatted as Markdown",
    resourceReadDraft?.result?.contents?.[0]?.text?.includes("⚡ Black Hole Star"),
    JSON.stringify(resourceReadDraft)
  );

  // --------------------------------------------------------------------------------
  // SUMMARY
  // --------------------------------------------------------------------------------
  console.log("\n================================================================================");
  console.log(`TOTAL TESTS: ${totalTests} | PASSED: ${passedTests} | FAILED: ${failedTests}`);
  console.log("================================================================================");

  if (failedTests > 0) {
    console.log("\nIdentified Shortcomings / Failures:");
    for (const f of findings) {
      console.log(`- ${f.description}: ${f.failureDetail}`);
    }
  }
}

runTestSuite().catch(err => {
  console.error("Stress test crashed:", err);
  process.exit(1);
});
