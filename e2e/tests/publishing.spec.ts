import { expect, test } from "@playwright/test";
import { loginViaUi } from "../helpers/auth.js";

test.describe("Publishing", () => {
  test.beforeEach(async ({ page }) => {
    await loginViaUi(page);
  });

  test("can open the Adapt modal", async ({ page }) => {
    await page.getByRole("button", { name: /adapt/i }).click();

    await expect(page.getByText(/adapt to other platforms/i)).toBeVisible();
  });
});