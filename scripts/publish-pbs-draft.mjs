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
    if (process.env[key] === undefined) process.env[key] = val;
  }
}

const rootDir = path.resolve(path.dirname(new URL(import.meta.url).pathname), "..");
loadEnvFile(path.join(rootDir, ".env.functional"));
loadEnvFile(path.join(rootDir, ".env"));

async function run() {
  const loginRes = await fetch("http://api:8080/api/auth/login", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ emailOrUsername: process.env.FUNCTIONAL_SW_USERNAME || "admin", password: process.env.FUNCTIONAL_SW_PASSWORD || "changeme" })
  });
  const { accessToken } = await loginRes.json();

  const draftId = "01a0537b-a9f5-73cb-8660-6027977fd793";
  const detailRes = await fetch(`http://api:8080/api/drafts/${draftId}`, {
    headers: { Authorization: "Bearer " + accessToken }
  });
  const draft = await detailRes.json();

  console.log(`=== Publishing PBS Space Time Draft: "${draft.title}" ===`);
  console.log(`Content:\n${draft.content}\n`);

  // Execute /publish command via Chat endpoint
  const chatRes = await fetch("http://api:8080/api/chat", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: "Bearer " + accessToken
    },
    body: JSON.stringify({
      draftId: draft.id,
      messages: [
        {
          role: "user",
          content: [
            { type: "text", text: "Publish this thread to Bluesky." }
          ]
        }

      ]
    })

  });

  const responseText = await chatRes.text();
  console.log("Chat Publish Stream Output:");
  console.log(responseText);

  // Inspect published posts
  const updatedDetailRes = await fetch(`http://api:8080/api/drafts/${draftId}`, {
    headers: { Authorization: "Bearer " + accessToken }
  });
  const updated = await updatedDetailRes.json();
  console.log(`\nUpdated Draft Status: ${updated.status} | Stage: ${updated.stage}`);
  if (updated.variants?.[0]?.posts?.length) {
    console.log(`\n✓ Successfully Published to Bluesky (${updated.variants[0].posts.length} posts):`);
    for (const p of updated.variants[0].posts) {
      console.log(`  - [Post ${p.segmentIndex}] ${p.url} (Remote: ${p.remoteId})`);
    }
  }
}

run().catch(e => console.error("Error:", e));
