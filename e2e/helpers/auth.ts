import { expect, Page } from "@playwright/test";

export async function loginViaUi(page: Page): Promise<void> {
  await page.goto("/");
  await page.getByPlaceholder("Email or username").fill(process.env.ADMIN_USERNAME ?? "admin");
  await page.locator('input[type="password"]').fill(process.env.ADMIN_PASSWORD ?? "changeme");

  const composer = page.getByPlaceholder(/Ask the assistant/i);
  const error = page.getByText(/Invalid credentials/i);

  for (let attempt = 0; attempt < 4; attempt++) {
    await page.getByRole("button", { name: "sign in" }).click();

    try {
      await expect(composer).toBeVisible({ timeout: 6000 });
      return;
    } catch {
      if (await error.isVisible().catch(() => false)) {
        await page.waitForTimeout(1000);
      }
    }
  }

  await expect(composer).toBeVisible({ timeout: 15000 });
}
