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

  const draftsRes = await fetch("http://api:8080/api/drafts", {
    headers: { Authorization: "Bearer " + accessToken }
  });
  const allDrafts = await draftsRes.json();

  const pbsDrafts = allDrafts.filter(d => 
    d.title?.includes("Black Hole") || 
    d.title?.includes("Galaxy") || 
    d.title?.includes("JWST") || 
    d.automationWorkflowId === "c5856d32-82d1-4aff-8379-cf522531a5b0"
  );

  console.log(`=== PBS Space Time Drafts Found: ${pbsDrafts.length} ===`);
  for (const d of pbsDrafts) {
    const detailRes = await fetch(`http://api:8080/api/drafts/${d.id}`, {
      headers: { Authorization: "Bearer " + accessToken }
    });
    const detail = await detailRes.json();
    console.log(`\n-------------------------------------------------------------`);
    console.log(`Draft ID: ${detail.id}`);
    console.log(`Title: "${detail.title}"`);
    console.log(`Stage: ${detail.stage} | Status: ${detail.status}`);
    console.log(`Sources: ${detail.sources?.length || 0}`);
    if (detail.sources?.length) {
      for (const s of detail.sources) {
        console.log(`  - [Source: ${s.kind}] ${s.reference}`);
      }
    }
    console.log(`Variants: ${detail.variants?.length || 0}`);
    if (detail.variants?.length) {
      for (const v of detail.variants) {
        console.log(`  - Variant Platform: ${v.platform} | Stage: ${v.stage} | Posts: ${v.posts?.length || 0}`);
        if (v.posts?.length) {
          for (const p of v.posts) {
            console.log(`    * [Segment ${p.segmentIndex}] ${p.url}`);
          }
        }
      }
    }
    console.log(`Content:\n${detail.content}`);
  }
}

run().catch(e => console.error("Error:", e));
