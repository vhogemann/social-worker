import { expect, test } from "@playwright/test";
import { loginViaUi, navigateToComposer } from "../helpers/auth.js";

test.describe("Drafts", () => {
  test.beforeEach(async ({ page }) => {
    page.on("console", (msg: any) => console.log("BROWSER CONSOLE:", msg.text()));
    page.on("pageerror", (err: any) => console.log("BROWSER ERROR:", err.message));
    await loginViaUi(page);
    await navigateToComposer(page);
  });

  test("drafts library page displays header and navigation", async ({ page }) => {
    await page.getByRole("link", { name: "Drafts", exact: true }).click();
    await expect(page.getByRole("heading", { name: "Drafts Library" })).toBeVisible({ timeout: 10000 });
  });

  test("can type content in the editor", async ({ page }) => {
    await page.locator(".cm-content").click();
    await page.evaluate(() => {
      const el = document.querySelector(".cm-content") as HTMLElement;
      if (el) el.focus();
    });
    await page.keyboard.insertText("Hello from Playwright!");

    await page.waitForTimeout(3000);
    await page.reload();
    await page.waitForTimeout(1000);
    await expect(page.locator(".cm-content")).toHaveText(/Hello from Playwright/);
  });
});