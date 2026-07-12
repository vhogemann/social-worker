import { expect, Page } from "@playwright/test";

export async function loginViaUi(page: Page): Promise<void> {
  await page.goto("/");
  await page.getByPlaceholder("Email or username").fill(process.env.ADMIN_USERNAME ?? "admin");
  await page.locator('input[type="password"]').fill(process.env.ADMIN_PASSWORD ?? "changeme");
  await page.getByRole("button", { name: "sign in" }).click();
  await expect(page.locator("header")).toContainText("social-worker");
}
