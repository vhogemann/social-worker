import { expect, test } from "@playwright/test";
import { loginViaUi, navigateToComposer } from "../helpers/auth.js";

test("admin can sign in through the UI", async ({ page }) => {
  await loginViaUi(page);
  await navigateToComposer(page);

  await expect(page.getByText("drafts", { exact: true })).toBeVisible();
});
