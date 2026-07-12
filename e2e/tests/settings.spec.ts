import { expect, test } from "@playwright/test";
import { loginViaUi } from "../helpers/auth.js";

test.describe("Settings: Providers", () => {
  test.beforeEach(async ({ page }) => {
    await loginViaUi(page);
  });

  test("can add an LLM provider", async ({ page }) => {
    await page.getByRole("button", { name: /settings/i }).click();
    await page.getByRole("button", { name: "providers" }).click();

    await page.getByPlaceholder(/e\.?g\.? openrouter claude/i).fill("OpenRouter Claude");
    await page.getByPlaceholder(/e\.?g\.? anthropic/i).fill("anthropic/claude-3.5-sonnet");
    await page.getByRole("button", { name: "add provider" }).click();

    await expect(page.getByText("OpenRouter Claude")).toBeVisible();
  });
});

test.describe("Settings: Connections", () => {
  test.beforeEach(async ({ page }) => {
    await loginViaUi(page);
  });

  test("can connect a Bluesky account", async ({ page }) => {
    await page.getByRole("button", { name: /settings/i }).click();
    await page.getByRole("button", { name: "connections" }).click();

    await page.getByPlaceholder(/user\.bsky\.social/i).fill("demo.bsky.social");
    await page.locator('input[type="password"]').nth(0).fill("test-app-password");
    await page.getByRole("button", { name: /save connection/i }).click();

    await expect(page.getByText("demo.bsky.social")).toBeVisible();
  });
});

test.describe("Settings: Brand Voices", () => {
  test.beforeEach(async ({ page }) => {
    await loginViaUi(page);
  });

  test("can create a brand voice prompt", async ({ page }) => {
    await page.getByRole("button", { name: /settings/i }).click();
    await page.getByRole("button", { name: "brand voices" }).click();

    await page.getByRole("button", { name: /new voice/i }).click();
    await page.locator('input[placeholder*="Friendly"]').fill("Professional Tone");
    await page.locator('textarea[placeholder*="relaxed"]').fill("Write in a clear, professional tone.");
    await page.getByRole("button", { name: /save brand voice/i }).click();

    await expect(page.getByText("Professional Tone")).toBeVisible();
  });
});