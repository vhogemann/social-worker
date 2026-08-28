import { expect, Page } from "@playwright/test";

export async function loginViaUi(page: Page, targetUrl = "/composer"): Promise<void> {
  await page.goto(targetUrl);

  const usernameInput = page.getByPlaceholder("Email or username");
  const isLoginPage = await usernameInput.isVisible({ timeout: 4000 }).catch(() => false);

  if (isLoginPage) {
    await usernameInput.fill(process.env.ADMIN_USERNAME ?? "admin");
    await page.locator('input[type="password"]').fill(process.env.ADMIN_PASSWORD ?? "changeme");
    await page.getByRole("button", { name: /sign in/i }).click();
  }

  await expect(page.getByRole("link", { name: "Workflows", exact: true })).toBeVisible({ timeout: 20000 });

  if (targetUrl.includes("/composer") || targetUrl.includes("/draft")) {
    await page.waitForURL(/\/draft/, { timeout: 15000 });
  }
}

export async function navigateToComposer(page: Page): Promise<void> {
  const composerInput = page.getByPlaceholder(/Ask the assistant/i);
  if (!page.url().includes("/draft/")) {
    await page.getByRole("link", { name: "Drafts", exact: true }).click();
    await page.waitForURL(/\/drafts/, { timeout: 15000 });

    const firstDraft = page.getByTestId("draft-card").first();
    if (await firstDraft.isVisible({ timeout: 3000 }).catch(() => false)) {
      await firstDraft.click();
    } else {
      await page.getByRole("button", { name: /Create Your First Draft|New Draft/i }).first().click();
      await page.getByPlaceholder(/e\.g\. Launch announcement/i).fill("E2E Draft");
      await page.getByRole("button", { name: "Create Draft" }).click();
    }
    await page.waitForURL(/\/draft\//, { timeout: 15000 });
  }
  await expect(composerInput).toBeVisible({ timeout: 15000 });
}
