import { expect, Page } from "@playwright/test";

export async function loginViaUi(page: Page, targetUrl = "/composer"): Promise<void> {
  await page.goto(targetUrl);

  const navBar = page.getByRole("link", { name: "Composer", exact: true });

  const usernameInput = page.getByPlaceholder("Email or username");
  const isLoginPage = await usernameInput.isVisible({ timeout: 4000 }).catch(() => false);

  if (isLoginPage) {
    await usernameInput.fill(process.env.ADMIN_USERNAME ?? "admin");
    await page.locator('input[type="password"]').fill(process.env.ADMIN_PASSWORD ?? "changeme");
    await page.getByRole("button", { name: /sign in/i }).click();
  }

  await expect(navBar).toBeVisible({ timeout: 20000 });

  if (targetUrl.includes("/composer")) {
    await page.waitForURL(/\/draft\//, { timeout: 15000 });
    await expect(page.getByText("drafts", { exact: true })).toBeVisible({ timeout: 15000 });
  }
}

export async function navigateToComposer(page: Page): Promise<void> {
  const composerInput = page.getByPlaceholder(/Ask the assistant/i);
  if (!page.url().includes("/draft/")) {
    await page.getByRole("link", { name: "Composer", exact: true }).click();
    await page.waitForURL(/\/draft\//, { timeout: 15000 });
  }
  await expect(composerInput).toBeVisible({ timeout: 15000 });
  await expect(page.getByText("drafts", { exact: true })).toBeVisible({ timeout: 15000 });
}
