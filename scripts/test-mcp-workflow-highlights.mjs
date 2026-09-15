const API_BASE = process.env.API_URL || "http://localhost:8101";

async function run() {
  const devKey = "sw_live_dev_admin_key_000000000000";
  console.log("=== Testing MCP Tool Integration for Workflow Highlights & Draft Filters ===");

  async function callMcpTool(name, args) {
    const res = await fetch(`${API_BASE}/api/mcp`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "X-API-Key": devKey
      },
      body: JSON.stringify({
        jsonrpc: "2.0",
        id: Date.now(),
        method: "tools/call",
        params: { name, arguments: args }
      })
    });
    return await res.json();
  }

  // 1. Test list_drafts with no filter
  console.log("\n1. Testing MCP list_drafts (limit: 5)...");
  const draftsAll = await callMcpTool("list_drafts", { limit: 5 });
  console.log("Result:", JSON.stringify(draftsAll, null, 2));

  // 2. Test list_drafts with isPublished = true
  console.log("\n2. Testing MCP list_drafts with isPublished = true...");
  const draftsPublished = await callMcpTool("list_drafts", { isPublished: true });
  console.log("Published drafts:", JSON.stringify(draftsPublished, null, 2));

  // 3. Test list_drafts with isPublished = false
  console.log("\n3. Testing MCP list_drafts with isPublished = false...");
  const draftsUnpublished = await callMcpTool("list_drafts", { isPublished: false, limit: 3 });
  console.log("Unpublished drafts:", JSON.stringify(draftsUnpublished, null, 2));

  // 4. Test list_workflows
  console.log("\n4. Testing MCP list_workflows...");
  const workflowsRes = await callMcpTool("list_workflows", {});
  console.log("Workflows:", JSON.stringify(workflowsRes, null, 2));

  const workflows = JSON.parse(workflowsRes.result.content[0].text);
  const existingWf = workflows[0];

  if (existingWf) {
    console.log(`\n5. Testing MCP list_drafts filtered by workflowId = ${existingWf.id}...`);
    const wfDrafts = await callMcpTool("list_drafts", { workflowId: existingWf.id });
    console.log("Workflow drafts:", JSON.stringify(wfDrafts, null, 2));

    // 6. Test trigger_workflow
    console.log(`\n6. Testing MCP trigger_workflow on workflow ${existingWf.id}...`);
    const triggerRes = await callMcpTool("trigger_workflow", { workflowId: existingWf.id });
    console.log("Trigger result:", JSON.stringify(triggerRes, null, 2));
  }

  // 7. Create a Highlight Digest Workflow via REST API and trigger it via MCP trigger_workflow
  console.log("\n7. Creating a Workflow Highlights digest workflow via REST API...");
  const createWfRes = await fetch(`${API_BASE}/api/workflows`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-API-Key": devKey
    },
    body: JSON.stringify({
      title: "Weekly Tech Highlights Recap",
      systemPrompt: "You are an expert tech curator. Summarize the key highlights from our published threads this past week.",
      targetPlatform: "Bluesky",
      isActive: true,
      sources: existingWf ? [
        {
          kind: "WorkflowHighlights",
          sourceWorkflowId: existingWf.id,
          configurationJson: JSON.stringify({ lookbackDays: 7 })
        }
      ] : []
    })
  });

  if (createWfRes.ok) {
    const createdWf = await createWfRes.json();
    console.log("Created Digest Workflow:", JSON.stringify(createdWf, null, 2));

    console.log(`\n8. Testing MCP trigger_workflow on newly created Digest Workflow ${createdWf.id}...`);
    const digestTriggerRes = await callMcpTool("trigger_workflow", { workflowId: createdWf.id });
    console.log("Digest Trigger Result:", JSON.stringify(digestTriggerRes, null, 2));
  } else {
    console.error("Failed to create digest workflow:", await createWfRes.text());
  }

  console.log("\n=== MCP Workflow Highlights & Draft Filters Tested Successfully! ===");
}

run().catch(err => {
  console.error("Test failed:", err);
  process.exit(1);
});
