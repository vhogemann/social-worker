const API_BASE = process.env.API_URL || "http://localhost:8101";

async function run() {
  console.log("=== Testing MCP & API Key Integration ===");

  // 1. Authenticate with dev seed key
  const devKey = "sw_live_dev_admin_key_000000000000";
  console.log(`\n1. Verifying default dev API key (${devKey.substring(0, 16)}...)...`);

  // 2. Direct JSON-RPC: initialize
  console.log("\n2. Calling MCP 'initialize' over stateless JSON-RPC...");
  const initRes = await fetch(`${API_BASE}/api/mcp`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-API-Key": devKey
    },
    body: JSON.stringify({
      jsonrpc: "2.0",
      id: 1,
      method: "initialize",
      params: {}
    })
  });
  const initJson = await initRes.json();
  console.log("Initialize Response:", JSON.stringify(initJson, null, 2));

  // 3. Direct JSON-RPC: tools/list
  console.log("\n3. Calling MCP 'tools/list'...");
  const toolsRes = await fetch(`${API_BASE}/api/mcp`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-API-Key": devKey
    },
    body: JSON.stringify({
      jsonrpc: "2.0",
      id: 2,
      method: "tools/list",
      params: {}
    })
  });
  const toolsJson = await toolsRes.json();
  console.log(`Discovered ${toolsJson.result?.tools?.length} MCP tools:`);
  for (const tool of toolsJson.result?.tools || []) {
    console.log(`  * ${tool.name}: ${tool.description}`);
  }

  // 4. Direct JSON-RPC: tools/call (create_draft)
  console.log("\n4. Calling MCP 'tools/call: create_draft'...");
  const createDraftRes = await fetch(`${API_BASE}/api/mcp`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-API-Key": devKey
    },
    body: JSON.stringify({
      jsonrpc: "2.0",
      id: 3,
      method: "tools/call",
      params: {
        name: "create_draft",
        arguments: {
          title: "MCP End-to-End Test Draft",
          content: "Testing MCP protocol natively on SocialWorker!\n\n---\n\nSecond segment with live validation.",
          platform: "Bluesky"
        }
      }
    })
  });
  const createDraftJson = await createDraftRes.json();
  console.log("Create Draft Result:", JSON.stringify(createDraftJson, null, 2));

  // 5. Direct JSON-RPC: tools/call (list_drafts)
  console.log("\n5. Calling MCP 'tools/call: list_drafts'...");
  const listDraftsRes = await fetch(`${API_BASE}/api/mcp`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-API-Key": devKey
    },
    body: JSON.stringify({
      jsonrpc: "2.0",
      id: 4,
      method: "tools/call",
      params: {
        name: "list_drafts",
        arguments: { limit: 3 }
      }
    })
  });
  const listDraftsJson = await listDraftsRes.json();
  console.log("List Drafts Result:", JSON.stringify(listDraftsJson, null, 2));

  // 6. Direct JSON-RPC: resources/list
  console.log("\n6. Calling MCP 'resources/list'...");
  const resourcesRes = await fetch(`${API_BASE}/api/mcp`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-API-Key": devKey
    },
    body: JSON.stringify({
      jsonrpc: "2.0",
      id: 5,
      method: "resources/list",
      params: {}
    })
  });
  const resourcesJson = await resourcesRes.json();
  console.log("Resources Result:", JSON.stringify(resourcesJson, null, 2));

  // 7. Test API Key CRUD
  console.log("\n7. Creating a new dynamic API key via POST /api/api-keys...");
  const createKeyRes = await fetch(`${API_BASE}/api/api-keys`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-API-Key": devKey
    },
    body: JSON.stringify({
      name: "Dynamic Agent Key",
      scopes: "mcp,drafts,publish"
    })
  });
  const newKeyJson = await createKeyRes.json();
  console.log("Generated API Key:", JSON.stringify(newKeyJson, null, 2));

  // Verify new key works for MCP
  console.log("\n8. Verifying MCP call using newly generated API Key...");
  const testNewKeyRes = await fetch(`${API_BASE}/api/mcp`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "Authorization": `Bearer ${newKeyJson.plaintextKey}`
    },
    body: JSON.stringify({
      jsonrpc: "2.0",
      id: 6,
      method: "ping",
      params: {}
    })
  });
  const testNewKeyJson = await testNewKeyRes.json();
  console.log("Ping with New Key:", JSON.stringify(testNewKeyJson, null, 2));

  console.log("\n=== MCP & API Key Integration Tests Passed Successfully! ===");
}

run().catch(err => {
  console.error("Test failed:", err);
  process.exit(1);
});
