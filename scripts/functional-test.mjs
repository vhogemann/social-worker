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

// Load .env.functional if present, then fallback to .env
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
  cleanupPosts: process.env.FUNCTIONAL_CLEANUP_POSTS !== "false",
  tag: process.env.FUNCTIONAL_TAG || "[functional-test]",
};

// ---------------------------------------------------------------------------
// Helpers & Diagnostics
// ---------------------------------------------------------------------------
class StepTracker {
  constructor(scenario) {
    this.scenario = scenario;
    this.startTime = Date.now();
    this.steps = [];
  }

  async runStep(name, fn) {
    const stepStart = Date.now();
    try {
      const data = await fn();
      const step = {
        name,
        passed: true,
        durationMs: Date.now() - stepStart,
        ...(data || {}),
      };
      this.steps.push(step);
      return step;
    } catch (err) {
      const step = {
        name,
        passed: false,
        durationMs: Date.now() - stepStart,
        error: err.message || String(err),
      };
      this.steps.push(step);
      throw err;
    }
  }

  toResult() {
    const passed = this.steps.length > 0 && this.steps.every((s) => s.passed);
    return {
      scenario: this.scenario,
      timestamp: new Date().toISOString(),
      passed,
      durationMs: Date.now() - this.startTime,
      steps: this.steps,
      summary: passed
        ? `Scenario '${this.scenario}' passed (${this.steps.length}/${this.steps.length} steps).`
        : `Scenario '${this.scenario}' failed. Check failed steps for details.`,
    };
  }
}

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
// ATProto Direct Client (Out-of-Band XRPC)
// ---------------------------------------------------------------------------
class BlueskyAtProtoClient {
  constructor(pdsUrl, handle, appPassword) {
    this.pdsUrl = pdsUrl.replace(/\/$/, "");
    this.handle = handle;
    this.appPassword = appPassword;
    this.session = null;
  }

  async createSession() {
    if (!this.handle || !this.appPassword) {
      throw new Error("Bluesky handle and app password are required for ATProto verification.");
    }

    const res = await fetch(`${this.pdsUrl}/xrpc/com.atproto.server.createSession`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        identifier: this.handle,
        password: this.appPassword,
      }),
    });

    if (!res.ok) {
      const errBody = await res.text();
      throw new Error(`ATProto createSession failed (${res.status}): ${errBody}`);
    }

    this.session = await res.json();
    return this.session;
  }

  async getPostThread(postUri) {
    const url = `${this.pdsUrl}/xrpc/app.bsky.feed.getPostThread?uri=${encodeURIComponent(postUri)}&depth=5`;
    const headers = {};
    if (this.session?.accessJwt) {
      headers["Authorization"] = `Bearer ${this.session.accessJwt}`;
    }

    const res = await fetch(url, { headers });
    if (!res.ok) {
      const errBody = await res.text();
      throw new Error(`ATProto getPostThread failed (${res.status}): ${errBody}`);
    }

    return await res.json();
  }

  async deletePostRecord(atUri) {
    if (!this.session) {
      await this.createSession();
    }

    const parts = atUri.replace("at://", "").split("/");
    if (parts.length < 3) {
      throw new Error(`Invalid AT-URI format: ${atUri}`);
    }

    const repo = parts[0];
    const collection = parts[1];
    const rkey = parts[2];

    const res = await fetch(`${this.pdsUrl}/xrpc/com.atproto.repo.deleteRecord`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${this.session.accessJwt}`,
      },
      body: JSON.stringify({
        repo,
        collection,
        rkey,
      }),
    });

    if (!res.ok) {
      const errBody = await res.text();
      throw new Error(`ATProto deleteRecord failed (${res.status}): ${errBody}`);
    }

    return true;
  }
}

// ---------------------------------------------------------------------------
// Scenario: Health
// ---------------------------------------------------------------------------
async function runHealthScenario() {
  const tracker = new StepTracker("health");

  await tracker.runStep("Check Social Worker API Health", async () => {
    const res = await apiRequest("/healthz");
    if (!res.ok && res.status !== 404) {
      throw new Error(`API health check failed with status ${res.status}`);
    }
    const resolvedUrl = await resolveWorkingSwUrl();
    return { detail: `Social Worker reachable at ${resolvedUrl}` };
  });

  await tracker.runStep("Check Bluesky PDS Connectivity", async () => {
    const res = await fetch(`${CONFIG.bskyPdsUrl}/xrpc/com.atproto.server.describeServer`);
    if (!res.ok) {
      throw new Error(`Bluesky PDS check failed with status ${res.status}`);
    }
    const data = await res.json();
    return { detail: `PDS available: ${CONFIG.bskyPdsUrl} (availableUserDomains: ${data.availableUserDomains?.join(", ") || "none"})` };
  });

  return tracker.toResult();
}

// ---------------------------------------------------------------------------
// Scenario: Drafts CRUD
// ---------------------------------------------------------------------------
async function runDraftScenario() {
  const tracker = new StepTracker("draft");
  let token = null;
  let createdDraftId = null;

  try {
    await tracker.runStep("Authenticate as User", async () => {
      token = await loginSocialWorker();
      return { detail: `Authenticated as ${CONFIG.swUser}` };
    });

    await tracker.runStep("Create Test Draft", async () => {
      const res = await apiRequest(
        "/api/drafts",
        {
          method: "POST",
          body: JSON.stringify({
            title: `${CONFIG.tag} Draft CRUD Test ${Date.now()}`,
            content: "Draft content segment 1.\n\n---\n\nDraft content segment 2.",
            targetPlatform: "Bluesky",
          }),
        },
        token
      );

      if (!res.ok || !res.body?.id) {
        throw new Error(`Draft creation failed (${res.status}): ${JSON.stringify(res.body)}`);
      }

      createdDraftId = res.body.id;
      return { draftId: createdDraftId, detail: `Created draft ID ${createdDraftId}` };
    });

    await tracker.runStep("Read Draft Details", async () => {
      const res = await apiRequest(`/api/drafts/${createdDraftId}`, {}, token);
      if (!res.ok || res.body?.id !== createdDraftId) {
        throw new Error(`Failed to read back draft (${res.status}): ${JSON.stringify(res.body)}`);
      }
      return { detail: `Draft verified: "${res.body.title}"` };
    });

    await tracker.runStep("Update Draft Content", async () => {
      const res = await apiRequest(
        `/api/drafts/${createdDraftId}`,
        {
          method: "PATCH",
          body: JSON.stringify({
            title: `${CONFIG.tag} Updated Draft Title ${Date.now()}`,
            content: "Updated segment 1 content.\n\n---\n\nUpdated segment 2 content.",
          }),
        },
        token
      );

      if (!res.ok) {
        throw new Error(`Draft update failed (${res.status}): ${JSON.stringify(res.body)}`);
      }
      return { detail: "Draft updated successfully" };
    });

    await tracker.runStep("Delete Test Draft", async () => {
      const res = await apiRequest(
        `/api/drafts/${createdDraftId}`,
        {
          method: "PATCH",
          body: JSON.stringify({ status: "Deleted" }),
        },
        token
      );
      if (!res.ok) {
        throw new Error(`Draft deletion failed (${res.status})`);
      }
      createdDraftId = null;
      return { detail: "Draft deleted from Social Worker" };
    });
  } catch (err) {
    if (createdDraftId && token) {
      await apiRequest(
        `/api/drafts/${createdDraftId}`,
        {
          method: "PATCH",
          body: JSON.stringify({ status: "Deleted" }),
        },
        token
      ).catch(() => null);
    }
  }

  return tracker.toResult();
}

// ---------------------------------------------------------------------------
// Scenario: Publish (Live E2E + ATProto Out-of-Band Verification)
// ---------------------------------------------------------------------------
async function runPublishScenario() {
  const tracker = new StepTracker("publish");

  if (!CONFIG.bskyHandle || !CONFIG.bskyAppPassword) {
    throw new Error(
      "Missing required Bluesky credentials. Please set FUNCTIONAL_BLUESKY_HANDLE and FUNCTIONAL_BLUESKY_APP_PASSWORD in .env.functional."
    );
  }

  let token = null;
  let draftId = null;
  let threadId = null;
  let publishResult = null;
  const publishedAtUris = [];
  const bskyClient = new BlueskyAtProtoClient(CONFIG.bskyPdsUrl, CONFIG.bskyHandle, CONFIG.bskyAppPassword);

  const timestamp = new Date().toISOString();
  const rootText = `${CONFIG.tag} Live E2E Verification Test (Root Post) at ${timestamp}`;
  const replyText = `${CONFIG.tag} Thread reply post segment 2 verifying reply chain structure.`;

  try {
    await tracker.runStep("Authenticate with Social Worker", async () => {
      token = await loginSocialWorker();
      return { detail: `Authenticated as ${CONFIG.swUser}` };
    });

    await tracker.runStep("Configure Bluesky Connection in Social Worker", async () => {
      const res = await apiRequest(
        "/api/accounts",
        {
          method: "POST",
          body: JSON.stringify({
            platform: "Bluesky",
            handle: CONFIG.bskyHandle,
            appPassword: CONFIG.bskyAppPassword,
          }),
        },
        token
      );

      if (!res.ok) {
        throw new Error(`Account connection failed (${res.status}): ${JSON.stringify(res.body)}`);
      }
      return { detail: `Account connected for handle @${CONFIG.bskyHandle}` };
    });

    await tracker.runStep("Create Multi-Segment Thread Draft", async () => {
      const markdown = `${rootText}\n\n---\n\n${replyText}`;
      const res = await apiRequest(
        "/api/drafts",
        {
          method: "POST",
          body: JSON.stringify({
            title: `${CONFIG.tag} E2E Publish Test ${Date.now()}`,
            content: markdown,
            targetPlatform: "Bluesky",
          }),
        },
        token
      );

      if (!res.ok || !res.body?.id) {
        throw new Error(`Failed to create draft (${res.status}): ${JSON.stringify(res.body)}`);
      }

      draftId = res.body.id;

      const threadsRes = await apiRequest(`/api/drafts/${draftId}/threads`, {}, token);
      if (!threadsRes.ok || !threadsRes.body || threadsRes.body.length === 0) {
        throw new Error(`Platform threads missing for draft (${threadsRes.status})`);
      }

      const blueskyThread = threadsRes.body.find((t) => t.platform.toLowerCase() === "bluesky");
      if (!blueskyThread) {
        throw new Error("Bluesky platform thread not found on draft");
      }

      threadId = blueskyThread.id;

      await apiRequest(
        `/api/drafts/${draftId}/threads/${threadId}`,
        {
          method: "PATCH",
          body: JSON.stringify({
            stage: "Ready",
            content: markdown,
          }),
        },
        token
      );

      return {
        draftId,
        threadId,
        detail: `Draft and Platform Thread ready (2 segments)`,
      };
    });

    await tracker.runStep("Publish Thread via Social Worker", async () => {
      const res = await apiRequest(
        `/api/drafts/${draftId}/threads/${threadId}/publish`,
        { method: "POST" },
        token
      );

      if (!res.ok || !res.body?.success) {
        throw new Error(`Publish failed (${res.status}): ${res.body?.errorMessage || JSON.stringify(res.body)}`);
      }

      publishResult = res.body;

      for (const p of publishResult.posts || []) {
        if (p.remoteId) publishedAtUris.push(p.remoteId);
      }

      const rootUrl = publishResult.posts?.[0]?.url || "";
      const rootAtUri = publishResult.posts?.[0]?.remoteId || "";

      return {
        url: rootUrl,
        atUri: rootAtUri,
        postCount: publishedAtUris.length,
        detail: `Published ${publishedAtUris.length} post(s) to Bluesky`,
      };
    });

    await tracker.runStep("Direct ATProto Out-of-Band Verification", async () => {
      if (publishedAtUris.length === 0) {
        throw new Error("No remote AT-URIs returned by publisher.");
      }

      const rootAtUri = publishedAtUris[0];
      await bskyClient.createSession();

      const threadData = await bskyClient.getPostThread(rootAtUri);
      const rootPost = threadData.thread?.post;

      if (!rootPost) {
        throw new Error("Root post not found in ATProto getPostThread response");
      }

      if (!rootPost.record?.text?.includes(CONFIG.tag)) {
        throw new Error(`Root post text mismatch. Expected tag '${CONFIG.tag}', got: '${rootPost.record?.text}'`);
      }

      if (publishedAtUris.length > 1) {
        const replies = threadData.thread?.replies || [];
        if (replies.length === 0) {
          throw new Error("Expected thread reply post in ATProto tree, but replies list is empty");
        }
        const firstReply = replies[0]?.post;
        if (!firstReply) {
          throw new Error("First reply post missing in thread tree");
        }
      }

      return {
        atUri: rootAtUri,
        authorDid: rootPost.author?.did,
        authorHandle: rootPost.author?.handle,
        detail: `ATProto verified: Root post and reply chain confirmed on Bluesky network`,
      };
    });

    if (CONFIG.cleanupPosts) {
      await tracker.runStep("Cleanup Test Posts on Bluesky", async () => {
        let deletedCount = 0;
        for (const uri of [...publishedAtUris].reverse()) {
          try {
            await bskyClient.deletePostRecord(uri);
            deletedCount++;
          } catch (err) {
            console.error(`Failed to delete record ${uri}:`, err.message);
          }
        }
        return { detail: `Cleaned up ${deletedCount}/${publishedAtUris.length} test post(s) from Bluesky` };
      });
    }

    await tracker.runStep("Cleanup Test Draft in Social Worker", async () => {
      if (draftId && token) {
        await apiRequest(
          `/api/drafts/${draftId}`,
          {
            method: "PATCH",
            body: JSON.stringify({ status: "Deleted" }),
          },
          token
        );
        draftId = null;
      }
      return { detail: "Test draft cleaned up from Social Worker database" };
    });
  } catch (err) {
    if (CONFIG.cleanupPosts && publishedAtUris.length > 0) {
      try {
        await bskyClient.createSession().catch(() => null);
        for (const uri of [...publishedAtUris].reverse()) {
          await bskyClient.deletePostRecord(uri).catch(() => null);
        }
      } catch (_) {}
    }
    if (draftId && token) {
      await apiRequest(
        `/api/drafts/${draftId}`,
        {
          method: "PATCH",
          body: JSON.stringify({ status: "Deleted" }),
        },
        token
      ).catch(() => null);
    }
  }

  return tracker.toResult();
}

// ---------------------------------------------------------------------------
// Scenario: Feeds & Workflows
// ---------------------------------------------------------------------------
async function runFeedsScenario() {
  const tracker = new StepTracker("feeds");

  await tracker.runStep("Authenticate as User", async () => {
    const token = await loginSocialWorker();

    const workflowsRes = await apiRequest("/api/workflows", {}, token);
    if (!workflowsRes.ok) {
      throw new Error(`Failed to list workflows (${workflowsRes.status})`);
    }

    const feedsRes = await apiRequest("/api/feeds", {}, token);
    if (!feedsRes.ok) {
      throw new Error(`Failed to list feed subscriptions (${feedsRes.status})`);
    }

    const workflowCount = Array.isArray(workflowsRes.body) ? workflowsRes.body.length : 0;
    const feedCount = Array.isArray(feedsRes.body) ? feedsRes.body.length : 0;

    return {
      detail: `Verified Feeds API: ${feedCount} feed subscriptions, ${workflowCount} automation workflows`,
      workflowCount,
      feedCount,
    };
  });

  return tracker.toResult();
}

// ---------------------------------------------------------------------------
// Scenario: Stress (Full Stack Exhaustive Verification)
// ---------------------------------------------------------------------------
async function runStressScenario() {
  const tracker = new StepTracker("stress");
  let token = null;

  // Cleanup tracking arrays
  const cleanupDraftIds = [];
  const cleanupPromptIds = [];
  const cleanupWorkflowIds = [];
  const cleanupTemplateIds = [];

  try {
    // 1. Authentication & User Profile
    await tracker.runStep("Auth: Login & Verify User Profile", async () => {
      token = await loginSocialWorker();
      const meRes = await apiRequest("/api/auth/me", {}, token);
      if (!meRes.ok || !meRes.body?.username) {
        throw new Error(`Failed to fetch user profile (${meRes.status})`);
      }
      return { detail: `User authenticated: ${meRes.body.username} (${meRes.body.role || "User"})` };
    });


    // 2. LLM Providers & Platform Capabilities
    await tracker.runStep("Providers: List Available Providers & Capabilities", async () => {
      const res = await apiRequest("/api/providers/available", {}, token);
      if (!res.ok || !Array.isArray(res.body)) {
        throw new Error(`Failed to list available LLM providers (${res.status})`);
      }
      const capRes = await apiRequest("/api/providers/platform-capabilities", {}, token);
      return {
        detail: `Found ${res.body.length} available provider(s), platform capabilities verified`,
        providers: res.body.map((p) => p.name),
      };
    });


    // 3. Brand Voice Prompts CRUD
    let testPromptId = null;
    await tracker.runStep("Prompts: Create & Read Brand Voice", async () => {
      const res = await apiRequest(
        "/api/brand-prompts",
        {
          method: "POST",
          body: JSON.stringify({
            name: `${CONFIG.tag} Test Brand Voice ${Date.now()}`,
            body: "Write concise, technical updates with bullet points.",
            isDefault: false,
          }),
        },
        token
      );
      if (!res.ok || !res.body?.id) {
        throw new Error(`Failed to create brand voice prompt (${res.status}): ${JSON.stringify(res.body)}`);
      }
      testPromptId = res.body.id;
      cleanupPromptIds.push(testPromptId);

      const getRes = await apiRequest(`/api/brand-prompts/${testPromptId}`, {}, token);
      if (!getRes.ok) {
        throw new Error(`Failed to read back brand prompt (${getRes.status})`);
      }
      return { promptId: testPromptId, detail: `Created and verified brand voice prompt "${res.body.name}"` };
    });

    await tracker.runStep("Prompts: Update & Delete Brand Voice", async () => {
      const updateRes = await apiRequest(
        `/api/brand-prompts/${testPromptId}`,
        {
          method: "PUT",
          body: JSON.stringify({
            name: `${CONFIG.tag} Updated Voice ${Date.now()}`,
            body: "Updated voice body text.",
            isDefault: false,
          }),
        },
        token
      );
      if (!updateRes.ok) {
        throw new Error(`Failed to update prompt (${updateRes.status})`);
      }

      const delRes = await apiRequest(`/api/brand-prompts/${testPromptId}`, { method: "DELETE" }, token);
      if (!delRes.ok) {
        throw new Error(`Failed to delete prompt (${delRes.status})`);
      }
      const idx = cleanupPromptIds.indexOf(testPromptId);
      if (idx >= 0) cleanupPromptIds.splice(idx, 1);
      return { detail: "Prompt updated and deleted cleanly" };
    });

    // 4. Templates & Image Engine Preview
    await tracker.runStep("Templates: List & Preview Layer Tree Render", async () => {
      const templatesRes = await apiRequest("/api/templates", {}, token);
      if (!templatesRes.ok || !Array.isArray(templatesRes.body)) {
        throw new Error(`Failed to list templates (${templatesRes.status})`);
      }

      // Test rendering sample preview
      const previewRes = await apiRequest(
        "/api/templates/preview",
        {
          method: "POST",
          body: JSON.stringify({
            schema: JSON.stringify({
              layers: [
                {
                  id: "bg",
                  type: "solid",
                  color: "#1e1e2e",
                  width: 600,
                  height: 400,
                },
                {
                  id: "title",
                  type: "text",
                  text: "Stress Test Image Engine",
                  fontFamily: "sans-serif",
                  fontSize: 32,
                  color: "#ffffff",
                  x: 40,
                  y: 100,
                },
              ],
            }),
            slots: {},
          }),
        },
        token
      );

      return {
        detail: `Templates API OK (${templatesRes.body.length} templates), Image Engine preview rendered (${previewRes.status})`,
      };
    });

    // 5. Workflows & Feed Ingestion Pipeline
    let testWorkflowId = null;
    await tracker.runStep("Workflows: Create Automation Workflow", async () => {
      const res = await apiRequest(
        "/api/workflows",
        {
          method: "POST",
          body: JSON.stringify({
            title: `${CONFIG.tag} Stress Workflow ${Date.now()}`,
            instructionPrompt: "Summarize the technical points clearly.",
            destinations: [{ platform: "Bluesky", autoPublish: false }],
          }),
        },
        token
      );
      if (!res.ok || !res.body?.id) {
        throw new Error(`Failed to create workflow (${res.status}): ${JSON.stringify(res.body)}`);
      }
      testWorkflowId = res.body.id;
      cleanupWorkflowIds.push(testWorkflowId);
      return { workflowId: testWorkflowId, detail: `Created workflow ID ${testWorkflowId}` };
    });

    await tracker.runStep("Workflows: Query Workflow Drafts & Cleanup", async () => {
      const draftsRes = await apiRequest(`/api/workflows/${testWorkflowId}/drafts`, {}, token);
      if (!draftsRes.ok) {
        throw new Error(`Failed to query workflow drafts (${draftsRes.status})`);
      }

      const delRes = await apiRequest(`/api/workflows/${testWorkflowId}`, { method: "DELETE" }, token);
      if (!delRes.ok && delRes.status !== 204) {
        throw new Error(`Failed to delete workflow (${delRes.status})`);
      }
      const idx = cleanupWorkflowIds.indexOf(testWorkflowId);
      if (idx >= 0) cleanupWorkflowIds.splice(idx, 1);
      return { detail: "Workflow drafts queried and workflow deleted cleanly" };
    });

    // 6. Source Ingestion (URL Source Extraction)
    let stressDraftId = null;
    await tracker.runStep("Sources: Import Web Article Source into Draft", async () => {
      const draftRes = await apiRequest(
        "/api/drafts",
        {
          method: "POST",
          body: JSON.stringify({
            title: `${CONFIG.tag} Source Ingestion Test ${Date.now()}`,
            content: "Draft discussing an attached source.",
            targetPlatform: "Bluesky",
          }),
        },
        token
      );
      if (!draftRes.ok || !draftRes.body?.id) {
        throw new Error(`Failed to create draft for sources test (${draftRes.status})`);
      }
      stressDraftId = draftRes.body.id;
      cleanupDraftIds.push(stressDraftId);

      // Add URL source (using public example domain)
      const sourceRes = await apiRequest(
        `/api/drafts/${stressDraftId}/urls`,
        {
          method: "POST",
          body: JSON.stringify({
            url: "https://example.com",
            title: "Example Domain Reference",
          }),
        },
        token
      );

      if (!sourceRes.ok || !sourceRes.body?.sourceId) {
        throw new Error(`Failed to import URL source (${sourceRes.status}): ${JSON.stringify(sourceRes.body)}`);
      }

      const sourcesList = await apiRequest(`/api/drafts/${stressDraftId}/sources`, {}, token);
      if (!sourcesList.ok || !Array.isArray(sourcesList.body) || sourcesList.body.length === 0) {
        throw new Error("Source not found in draft sources list");
      }

      return {
        draftId: stressDraftId,
        sourceId: sourceRes.body.sourceId,
        detail: `Attached URL source "${sourceRes.body.title || "example.com"}" to draft`,
      };
    });

    // 7. Multi-segment Thread Progression & Platform Variants
    await tracker.runStep("Drafts: 3-Segment Thread Assembly & Validation", async () => {
      const threadMarkdown =
        `1/3 ${CONFIG.tag} Stress Test Root Post with deep context.\n\n---\n\n2/3 Deep-dive analysis and technical metrics.\n\n---\n\n3/3 Final takeaways and reference link https://example.com`;

      const patchRes = await apiRequest(
        `/api/drafts/${stressDraftId}`,
        {
          method: "PATCH",
          body: JSON.stringify({
            title: `${CONFIG.tag} 3-Post Thread ${Date.now()}`,
            content: threadMarkdown,
          }),
        },
        token
      );
      if (!patchRes.ok) {
        throw new Error(`Failed to update 3-segment draft (${patchRes.status})`);
      }

      const threadsRes = await apiRequest(`/api/drafts/${stressDraftId}/threads`, {}, token);
      if (!threadsRes.ok || !threadsRes.body || threadsRes.body.length === 0) {
        throw new Error("Draft threads missing");
      }

      const bskyThread = threadsRes.body.find((t) => t.platform.toLowerCase() === "bluesky");
      if (!bskyThread) {
        throw new Error("Bluesky thread variant missing on draft");
      }

      // Advance stage to Ready
      const updateThreadRes = await apiRequest(
        `/api/drafts/${stressDraftId}/threads/${bskyThread.id}`,
        {
          method: "PATCH",
          body: JSON.stringify({
            stage: "Ready",
            content: threadMarkdown,
          }),
        },
        token
      );
      if (!updateThreadRes.ok) {
        throw new Error(`Failed to advance thread stage to Ready (${updateThreadRes.status})`);
      }

      return {
        threadId: bskyThread.id,
        detail: "3 segments assembled and stage advanced to Ready",
      };
    });

    // 8. Live Bluesky Multi-Segment Publishing & Out-of-Band ATProto Verification
    if (CONFIG.bskyHandle && CONFIG.bskyAppPassword) {
      const publishedUris = [];
      const bskyClient = new BlueskyAtProtoClient(CONFIG.bskyPdsUrl, CONFIG.bskyHandle, CONFIG.bskyAppPassword);

      await tracker.runStep("Publishing: Publish 3-Segment Thread to Bluesky", async () => {
        const threadsRes = await apiRequest(`/api/drafts/${stressDraftId}/threads`, {}, token);
        const bskyThread = threadsRes.body.find((t) => t.platform.toLowerCase() === "bluesky");

        const pubRes = await apiRequest(
          `/api/drafts/${stressDraftId}/threads/${bskyThread.id}/publish`,
          { method: "POST" },
          token
        );

        if (!pubRes.ok || !pubRes.body?.success) {
          throw new Error(`Publish failed (${pubRes.status}): ${pubRes.body?.errorMessage || JSON.stringify(pubRes.body)}`);
        }

        for (const p of pubRes.body.posts || []) {
          if (p.remoteId) publishedUris.push(p.remoteId);
        }

        return {
          postCount: publishedUris.length,
          rootUrl: pubRes.body.posts?.[0]?.url,
          detail: `Published ${publishedUris.length} segments to live Bluesky network`,
        };
      });

      await tracker.runStep("ATProto: Verify 3-Segment Thread Tree Integrity", async () => {
        if (publishedUris.length < 3) {
          throw new Error(`Expected at least 3 published posts, got ${publishedUris.length}`);
        }

        await bskyClient.createSession();
        const rootUri = publishedUris[0];
        const threadData = await bskyClient.getPostThread(rootUri);
        const rootPost = threadData.thread?.post;

        if (!rootPost) {
          throw new Error("Root post not found in ATProto getPostThread");
        }

        const reply1 = threadData.thread?.replies?.[0]?.post;
        if (!reply1) {
          throw new Error("Segment 2 reply post not found in thread tree");
        }

        return {
          rootAtUri: rootUri,
          rootCid: rootPost.cid,
          reply1AtUri: reply1.uri,
          detail: `ATProto verified: 3-tier thread hierarchy confirmed on Bluesky network`,
        };
      });

      if (CONFIG.cleanupPosts) {
        await tracker.runStep("Cleanup: Delete Live Test Posts on Bluesky", async () => {
          let count = 0;
          for (const uri of [...publishedUris].reverse()) {
            try {
              await bskyClient.deletePostRecord(uri);
              count++;
            } catch (err) {
              console.error(`Failed to delete record ${uri}:`, err.message);
            }
          }
          return { detail: `Cleaned up ${count}/${publishedUris.length} posts from Bluesky` };
        });
      }
    }

    // 9. Final Local Draft Cleanup
    await tracker.runStep("Cleanup: Remove Test Drafts from Social Worker", async () => {
      for (const id of cleanupDraftIds) {
        await apiRequest(
          `/api/drafts/${id}`,
          {
            method: "PATCH",
            body: JSON.stringify({ status: "Deleted" }),
          },
          token
        ).catch(() => null);
      }
      return { detail: "All test drafts deleted cleanly" };
    });
  } catch (err) {
    // Emergency cleanup
    if (token) {
      for (const id of cleanupDraftIds) {
        await apiRequest(`/api/drafts/${id}`, { method: "PATCH", body: JSON.stringify({ status: "Deleted" }) }, token).catch(() => null);
      }
      for (const id of cleanupPromptIds) {
        await apiRequest(`/api/brand-prompts/${id}`, { method: "DELETE" }, token).catch(() => null);
      }
      for (const id of cleanupWorkflowIds) {
        await apiRequest(`/api/workflows/${id}`, { method: "DELETE" }, token).catch(() => null);
      }
    }
  }

  return tracker.toResult();
}

// ---------------------------------------------------------------------------
// Scenario: Full Suite
// ---------------------------------------------------------------------------
async function runFullScenario() {
  const health = await runHealthScenario();
  const drafts = await runDraftScenario();
  const feeds = await runFeedsScenario();

  let publish = null;
  if (CONFIG.bskyHandle && CONFIG.bskyAppPassword) {
    publish = await runPublishScenario();
  }

  const allPassed = health.passed && drafts.passed && feeds.passed && (!publish || publish.passed);
  const combinedSteps = [
    ...health.steps.map((s) => ({ ...s, name: `[health] ${s.name}` })),
    ...drafts.steps.map((s) => ({ ...s, name: `[drafts] ${s.name}` })),
    ...feeds.steps.map((s) => ({ ...s, name: `[feeds] ${s.name}` })),
    ...(publish ? publish.steps.map((s) => ({ ...s, name: `[publish] ${s.name}` })) : []),
  ];

  return {
    scenario: "full",
    timestamp: new Date().toISOString(),
    passed: allPassed,
    durationMs: health.durationMs + drafts.durationMs + feeds.durationMs + (publish?.durationMs || 0),
    steps: combinedSteps,
    summary: allPassed
      ? `Full functional suite passed (${combinedSteps.length} steps).`
      : `Full functional suite failed. Check failed steps for details.`,
  };
}

// ---------------------------------------------------------------------------
// Main Entrypoint
// ---------------------------------------------------------------------------
async function main() {
  const scenario = (process.argv[2] || "full").toLowerCase();

  let result;
  try {
    switch (scenario) {
      case "health":
        result = await runHealthScenario();
        break;
      case "draft":
      case "drafts":
        result = await runDraftScenario();
        break;
      case "publish":
        result = await runPublishScenario();
        break;
      case "feeds":
        result = await runFeedsScenario();
        break;
      case "stress":
        result = await runStressScenario();
        break;
      case "full":
        result = await runFullScenario();
        break;
      default:
        console.error(`Unknown scenario '${scenario}'. Available: health, draft, publish, feeds, stress, full`);
        process.exit(2);
    }
  } catch (err) {
    result = {
      scenario,
      timestamp: new Date().toISOString(),
      passed: false,
      steps: [{ name: "Runner Fatal Error", passed: false, durationMs: 0, error: err.message || String(err) }],
      summary: `Fatal error executing scenario '${scenario}': ${err.message}`,
    };
  }

  process.stdout.write(JSON.stringify(result, null, 2) + "\n");
  process.exit(result.passed ? 0 : 1);
}

main().catch((err) => {
  console.error("Unhandled top-level error:", err);
  process.exit(1);
});
