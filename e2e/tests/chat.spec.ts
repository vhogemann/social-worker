import { expect, test } from "@playwright/test";
import { loginViaUi } from "../helpers/auth.js";

test.describe("Chat", () => {
  test.beforeEach(async ({ page }) => {
    await loginViaUi(page);
  });

  test("can send a message and receive a response", async ({ page }) => {
    const input = page.getByPlaceholder(/Ask the assistant/i);
    await input.fill("Hello, can you help me with my draft?");
    await page.getByRole("button", { name: "Send" }).click();

    await expect(page.getByText("assistant").first()).toBeVisible({ timeout: 10000 });
    await expect(page.getByText(/help|draft|assistant/i).first()).toBeVisible({ timeout: 5000 });
  });

  test("slash validate returns draft validation report", async ({ page }) => {
    const input = page.getByPlaceholder(/Ask the assistant/i);
    await input.fill("/validate");
    await page.getByRole("button", { name: "Send" }).click();

    await expect(page.getByText(/Draft Validation Report/i).first()).toBeVisible({ timeout: 10000 });
    await expect(page.getByText(/Overall Status/i).first()).toBeVisible({ timeout: 10000 });
  });

  test("slash help shows curated commands", async ({ page }) => {
    const input = page.getByPlaceholder(/Ask the assistant/i);
    await input.fill("/help");
    await page.getByRole("button", { name: "Send" }).click();

    await expect(page.getByText(/Available slash commands/i).first()).toBeVisible({ timeout: 10000 });
    await expect(page.getByText(/\/search <query>/i).first()).toBeVisible({ timeout: 10000 });
    await expect(page.getByText(/\/search-image <query>/i).first()).toBeVisible({ timeout: 10000 });
    await expect(page.getByText(/\/tool <tool_name>/i)).toHaveCount(0);
  });

  test("slash search-image without query returns usage", async ({ page }) => {
    const input = page.getByPlaceholder(/Ask the assistant/i);
    await input.fill("/search-image");
    await page.getByRole("button", { name: "Send" }).click();

    await expect(page.getByText(/Usage: \/search-image <query>/i).first()).toBeVisible({ timeout: 10000 });
  });
});