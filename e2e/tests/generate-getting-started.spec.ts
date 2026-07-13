import { expect, test, Page, Locator } from "@playwright/test";
import { loginViaUi } from "../helpers/auth.js";
import { loginAsAdmin } from "../helpers/api.js";
import { seedProvider, seedBrandVoice, seedDraft } from "../helpers/seed.js";
import * as fs from "fs";

const OUTPUT_DIR = "/e2e/output";
const OUTPUT_SCREENSHOT_DIR = `${OUTPUT_DIR}/getting-started`;
const COMMITTED_SCREENSHOT_DIR = "docs/getting-started";

interface Step {
  slug: string;
  title: string;
  description: string;
  fn: () => Promise<void>;
  target?: () => Locator;
}

async function screenshot(page: Page, slug: string, target?: Locator): Promise<string> {
  fs.mkdirSync(OUTPUT_SCREENSHOT_DIR, { recursive: true });
  const path = `${OUTPUT_SCREENSHOT_DIR}/${slug}.png`;
  if (target) {
    await target.screenshot({ path });
  } else {
    await page.screenshot({ path, fullPage: false });
  }
  return path;
}

test("generate getting-started guide", async ({ page }) => {
  const token = await loginAsAdmin();
  await seedProvider(token);
  await seedBrandVoice(token);
  const guideDraftTitle = `Getting Started Walkthrough ${Date.now()}`;
  await seedDraft(
    token,
    guideDraftTitle,
    "Launch stronger social posts with a repeatable workflow.\n\n---\n\nUse the editor, assistant, sources, and adaptation flow together.\n\n---\n\nFinish by previewing the thread before publishing."
  );

  await loginViaUi(page);
  await expect(page.getByText("drafts").first()).toBeVisible({ timeout: 10000 });

  const createModal = () => page.getByTestId("create-draft-modal");
  const adaptModal = () => page.getByTestId("adapt-variants-modal");
  const settingsModal = () => page.getByTestId("settings-modal");
  const chatPanel = () => page.getByTestId("chat-panel");
  const editorPanel = () => page.getByTestId("editor-panel");
  const sourcePreview = () => page.getByTestId("source-preview-modal");
  const threadPreview = () => page.getByTestId("thread-preview");
  const previewText = "Guide notes for social-worker. This reference exists to make the getting-started guide screenshots more distinct.";

  const steps: Step[] = [
    {
      slug: "01-login",
      title: "Workspace Overview",
      description: "After signing in, you land in the main workspace with drafts, editor, preview controls, and chat side by side.",
      fn: async () => {
        await page.getByText(guideDraftTitle).click();
        await expect(page.locator(".cm-content")).toContainText("Launch stronger social posts");
      },
    },
    {
      slug: "02-draft-list",
      title: "Create Draft Modal",
      description: "Use the drafts sidebar to create a new draft and choose the target platform up front.",
      fn: async () => {
        await page.getByRole("button", { name: /new/i }).click();
        await expect(page.getByText("Create New Draft")).toBeVisible();
      },
      target: createModal,
    },
    {
      slug: "03-editor",
      title: "Editor",
      description: "Write thread content in the markdown editor. Separate posts with `---` on its own line and keep refining in place.",
      fn: async () => {
        await page.getByRole("button", { name: /cancel/i }).click();
        await page.locator(".cm-content").click();
        await expect(page.locator(".cm-content")).toBeVisible();
      },
      target: editorPanel,
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
      target: chatPanel,
    },
    {
      slug: "05-sources",
      title: "Source Preview",
      description: "Open Sources to attach reference material, then preview extracted contents without leaving the editor.",
      fn: async () => {
        await page.getByRole("button", { name: /sources/i }).click();
        await page.locator("#attach-source-file").setInputFiles({
          name: "guide-notes.txt",
          mimeType: "text/plain",
          buffer: Buffer.from(previewText)
        });
        await expect(page.getByText("File: guide-notes.txt")).toBeVisible({ timeout: 10000 });
        await page.getByTitle("Preview source content").first().click({ force: true });
        await expect(page.getByText("Guide notes for social-worker.", { exact: false })).toBeVisible({ timeout: 10000 });
      },
      target: sourcePreview,
    },
    {
      slug: "06-adapt-modal",
      title: "Adapt Modal",
      description: "Click Adapt to generate platform-specific variants of your thread content.",
      fn: async () => {
        await page.getByRole("button", { name: /^close$/i }).click();
        await page.getByRole("button", { name: /adapt/i }).click();
        await expect(page.getByText(/adapt to other platforms/i)).toBeVisible();
      },
      target: adaptModal,
    },
    {
      slug: "07-publish",
      title: "Preview Mode",
      description: "Switch to Preview mode to inspect how the thread will read as a sequence of social posts before publishing.",
      fn: async () => {
        await page.getByRole("button", { name: /close/i }).click();
        await page.getByRole("button", { name: /^preview$/i }).click();
        await expect(page.getByText(/Launch stronger social posts/i).first()).toBeVisible();
      },
      target: threadPreview,
    },
    {
      slug: "08-settings",
      title: "Settings",
      description: "Open Settings to manage providers, connected accounts, and brand voice configuration from one place.",
      fn: async () => {
        await page.getByRole("button", { name: /settings/i }).click();
        await page.getByRole("button", { name: /providers/i }).click();
        await expect(page.getByText(/llm provider configuration/i)).toBeVisible();
      },
      target: settingsModal,
    },
  ];

  for (const step of steps) {
    await step.fn();
    await page.waitForTimeout(300);
    await screenshot(page, step.slug, step.target?.());
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
docker compose -f docker-compose.e2e.yml run --rm e2e npx playwright test

# Generate this guide
docker compose -f docker-compose.e2e.yml run --rm e2e npx playwright test tests/generate-getting-started.spec.ts
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