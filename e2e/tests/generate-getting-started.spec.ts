import { expect, test, Page } from "@playwright/test";
import { loginViaUi } from "../helpers/auth.js";
import { apiFetchAsUser, loginAsAdmin } from "../helpers/api.js";
import { seedProvider, seedBrandVoice } from "../helpers/seed.js";
import * as fs from "fs";

const SCREENSHOT_DIR = "/e2e/screenshots";
const OUTPUT_DIR = "/e2e/output";

interface Step {
  name: string;
  description: string;
  fn: () => Promise<void>;
}

async function screenshot(page: Page, name: string): Promise<string> {
  const path = `${SCREENSHOT_DIR}/${name}.png`;
  await page.screenshot({ path, fullPage: false });
  return path;
}

test("generate getting-started guide", async ({ page }) => {
  // --- Setup: seed provider for demo LLM ---
  const token = await loginAsAdmin();
  await seedProvider(token);
  await seedBrandVoice(token);

  // --- Login via UI ---
  await loginViaUi(page);
  await page.waitForTimeout(1000);

  const steps: Step[] = [
    {
      name: "01-login",
      description: "Sign in with your admin credentials. The app uses JWT authentication with refresh tokens.",
      fn: async () => { /* already done in loginViaUi */ },
    },
    {
      name: "02-draft-list",
      description: "The sidebar shows all your drafts. Click a draft to open it in the editor. Use the + button to create new drafts.",
      fn: async () => {
        await page.getByText("drafts").click();
      },
    },
    {
      name: "03-editor",
      description: "The markdown editor uses CodeMirror 6 with Vim keybindings. Write your thread content here. Use `---` on its own line to separate thread segments.",
      fn: async () => {
        await page.locator(".cm-content").click();
      },
    },
    {
      name: "04-chat",
      description: "The chat panel lets you collaborate with an AI assistant. Ask for improvements, rewrites, or new ideas. The assistant can read your draft and suggest changes.",
      fn: async () => {
        const input = page.getByPlaceholder("Ask the assistant...");
        await input.fill("Can you review my draft and suggest improvements?");
        await page.getByRole("button", { name: "Send" }).click();
        await expect(page.getByText("assistant").first()).toBeVisible({ timeout: 10000 });
        await expect(page.getByText(/stage proposal/i).first()).toBeVisible({ timeout: 10000 });
      },
    },
    {
      name: "05-sources",
      description: "Add reference sources to your draft. Paste URLs or upload files, and the assistant can reference them when generating content.",
      fn: async () => {
        await page.getByRole("button", { name: /sources/i }).click();
      },
    },
    {
      name: "06-adapt-modal",
      description: "Click 'Adapt' to generate platform-specific versions of your draft. The assistant tailors content for each platform's constraints (character limits, tone, format).",
      fn: async () => {
        await page.getByRole("button", { name: /adapt/i }).click();
        await expect(page.getByText(/adapt to other platforms/i)).toBeVisible();
      },
    },
    {
      name: "07-publish",
      description: "When your draft is ready, click Publish. The app posts to your connected Bluesky account. Publishing to Twitter, LinkedIn, Facebook, and Instagram is coming soon.",
      fn: async () => {
        await page.getByRole("button", { name: /close/i }).click();
      },
    },
    {
      name: "08-settings",
      description: "Open Settings to configure LLM providers (OpenAI, OpenRouter, Ollama), connect social media accounts, and manage brand voice prompts.",
      fn: async () => {
        await page.getByRole("button", { name: /settings/i }).click();
        await expect(page.getByText("settings").first()).toBeVisible();
      },
    },
  ];

  for (const step of steps) {
    await step.fn();
    await page.waitForTimeout(500);
    await screenshot(page, step.name);
  }

  // --- Generate GETTING_STARTED.md ---
  const relativePath = (name: string) => `e2e/screenshots/${name}.png`;

  const md = `# Getting Started with social-worker

social-worker is a local-first, Docker-only multi-modal assistant for composing and publishing social media threads.

## Prerequisites

- Docker with Compose v2
- mkcert (for local HTTPS)

## Quick Start

\`\`\`bash
git clone <repo>
cd social-worker
cp .env.example .env
./scripts/bootstrap.sh
docker compose up --build
\`\`\`

Then open \`https://social-worker.localtest\` in your browser.

## App Walkthrough

### 1. Login

${steps.map((s, i) => `### ${i + 1}. ${s.description.split(".")[0]}`).join("\n\n")}

---

${steps.map((s, i) => `### Step ${i + 1}: ${s.name.replace(/^\d+-/, "").replace(/-/g, " ").replace(/\b\w/g, (l) => l.toUpperCase())}

${s.description}

![${s.name}](${relativePath(s.name)})
`).join("\n\n")}

## Running E2E Tests

\`\`\`bash
# Run the full E2E suite
docker compose --profile e2e run --rm e2e npx playwright test

# Generate this guide
docker compose --profile e2e run --rm e2e npx playwright test tests/generate-getting-started.spec.ts
\`\`\`

## Regenerating This Guide

Run the \`generate-getting-started\` test after any UI changes that affect the screenshots:

\`\`\`bash
docker compose --profile e2e build e2e
docker compose --profile e2e run --rm e2e npx playwright test tests/generate-getting-started.spec.ts
cp e2e/output/GETTING_STARTED.md GETTING_STARTED.md
\`\`\`
`;

  fs.writeFileSync(`${OUTPUT_DIR}/GETTING_STARTED.md`, md);
  console.log(`GETTING_STARTED.md generated at ${OUTPUT_DIR}/GETTING_STARTED.md`);
  console.log(`Screenshots saved to ${SCREENSHOT_DIR}/`);
});