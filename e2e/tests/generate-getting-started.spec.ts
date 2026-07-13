import { expect, test, Page } from "@playwright/test";
import { loginViaUi } from "../helpers/auth.js";
import { loginAsAdmin } from "../helpers/api.js";
import { seedProvider, seedBrandVoice } from "../helpers/seed.js";
import * as fs from "fs";

const OUTPUT_DIR = "/e2e/output";
const OUTPUT_SCREENSHOT_DIR = `${OUTPUT_DIR}/getting-started`;
const COMMITTED_SCREENSHOT_DIR = "docs/getting-started";

interface Step {
  slug: string;
  title: string;
  description: string;
  fn: () => Promise<void>;
}

async function screenshot(page: Page, slug: string): Promise<string> {
  fs.mkdirSync(OUTPUT_SCREENSHOT_DIR, { recursive: true });
  const path = `${OUTPUT_SCREENSHOT_DIR}/${slug}.png`;
  await page.screenshot({ path, fullPage: false });
  return path;
}

test("generate getting-started guide", async ({ page }) => {
  const token = await loginAsAdmin();
  await seedProvider(token);
  await seedBrandVoice(token);

  await loginViaUi(page);
  await expect(page.getByText("drafts").first()).toBeVisible({ timeout: 10000 });

  const steps: Step[] = [
    {
      slug: "01-login",
      title: "Login",
      description: "Sign in with your admin credentials to open your workspace.",
      fn: async () => { /* already done in loginViaUi */ },
    },
    {
      slug: "02-draft-list",
      title: "Draft List",
      description: "Use the drafts sidebar to open an existing draft or create a new one with the + button.",
      fn: async () => {
        await page.getByText("drafts").click();
      },
    },
    {
      slug: "03-editor",
      title: "Editor",
      description: "Write thread content in the markdown editor. Separate posts with `---` on its own line.",
      fn: async () => {
        await page.locator(".cm-content").click();
      },
    },
    {
      slug: "04-chat",
      title: "Chat Assistant",
      description: "Use chat to ask for rewrites, improvements, and idea generation based on your current draft.",
      fn: async () => {
        const input = page.getByPlaceholder("Ask the assistant...");
        await input.fill("Can you review my draft and suggest improvements?");
        await page.getByRole("button", { name: "Send" }).click();
        await expect(page.getByText("assistant").first()).toBeVisible({ timeout: 15000 });
      },
    },
    {
      slug: "05-sources",
      title: "Sources",
      description: "Open Sources to add URLs or files that the assistant can use as references.",
      fn: async () => {
        await page.getByRole("button", { name: /sources/i }).click();
      },
    },
    {
      slug: "06-adapt-modal",
      title: "Adapt Modal",
      description: "Click Adapt to generate platform-specific variants of your thread content.",
      fn: async () => {
        await page.getByRole("button", { name: /adapt/i }).click();
        await expect(page.getByText(/adapt to other platforms/i)).toBeVisible();
      },
    },
    {
      slug: "07-publish",
      title: "Publish Controls",
      description: "Close the Adapt modal and review publishing controls in the editor toolbar.",
      fn: async () => {
        await page.getByRole("button", { name: /close/i }).click();
        await expect(page.getByRole("button", { name: /publish/i })).toBeVisible();
      },
    },
    {
      slug: "08-settings",
      title: "Settings",
      description: "Open Settings to configure LLM providers, connected accounts, and brand voice prompts.",
      fn: async () => {
        await page.getByRole("button", { name: /settings/i }).click();
        await expect(page.getByText("settings").first()).toBeVisible();
      },
    },
  ];

  for (const step of steps) {
    await step.fn();
    await page.waitForTimeout(300);
    await screenshot(page, step.slug);
  }

  fs.mkdirSync(OUTPUT_DIR, { recursive: true });
  const relativePath = (name: string) => `${COMMITTED_SCREENSHOT_DIR}/${name}.png`;

  const md = `# Getting Started with social-worker

social-worker is a local-first, Docker-only assistant for drafting and publishing social media threads.

## Prerequisites

- Docker with Compose v2

## Quick Start

\`\`\`bash
git clone <repo>
cd social-worker
cp .env.example .env
docker compose up --build
\`\`\`

Then open \`http://localhost:8100\` in your browser.

## Quick Tour

${steps.map((s, i) => `${i + 1}. **${s.title}**: ${s.description}`).join("\n")}

## Step-by-Step Walkthrough

${steps.map((s, i) => `### Step ${i + 1}: ${s.title}

${s.description}

![${s.slug}](${relativePath(s.slug)})
`).join("\n\n")}

## Running E2E Tests

\`\`\`bash
# Run the full E2E suite
docker compose --profile e2e run --rm e2e npx playwright test

# Generate this guide
docker compose --profile e2e run --rm e2e npx playwright test tests/generate-getting-started.spec.ts
\`\`\`

## Regenerating This Guide

Run the helper script to regenerate both this markdown file and the committed screenshots:

\`\`\`bash
./scripts/regenerate-getting-started.sh
\`\`\`
`;

  fs.writeFileSync(`${OUTPUT_DIR}/GETTING_STARTED.md`, md);
  console.log(`GETTING_STARTED.md generated at ${OUTPUT_DIR}/GETTING_STARTED.md`);
  console.log(`Screenshots saved to ${OUTPUT_SCREENSHOT_DIR}/`);
});