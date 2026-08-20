import { expect, test } from "@playwright/test";
import { loginViaUi } from "../helpers/auth.js";

test.describe("Feeds and Logs", () => {
  test.beforeEach(async ({ page }) => {
    await loginViaUi(page);
  });

  test("can navigate to feeds, create a subscription, and visit logs", async ({ page }) => {
    // Navigate to Feeds
    await page.getByRole("link", { name: "Feeds" }).click();
    await expect(page.getByRole("heading", { name: "Feed Automation" })).toBeVisible({ timeout: 10000 });

    // Open New Feed Modal
    await page.getByRole("button", { name: /New Feed/i }).click();
    await expect(page.getByRole("heading", { name: "Add Feed Subscription" })).toBeVisible();

    // Fill form
    const uniqueTitle = `Test Feed ${Date.now()}`;
    await page.getByPlaceholder(/e\.g\. \.NET Blog RSS/i).fill(uniqueTitle);
    await page.getByPlaceholder(/https:\/\/example\.com\/rss\.xml/i).fill("https://example.com/rss.xml");
    await page.getByRole("button", { name: "Create Subscription" }).click();

    // Verify feed card appears in grid
    await expect(page.getByText(uniqueTitle)).toBeVisible({ timeout: 10000 });

    // Navigate to Logs
    await page.getByRole("link", { name: "Logs" }).click();
    await expect(page.getByRole("heading", { name: /Processing Logs & Backlog/i })).toBeVisible({ timeout: 10000 });
  });
});
