import { expect, test } from "@playwright/test";
import { loginViaUi } from "../helpers/auth.js";

test.describe("Automation Workflows Dashboard", () => {
  test.beforeEach(async ({ page }) => {
    await loginViaUi(page, "/");
  });

  test("dashboard loads and displays automation workflows header", async ({ page }) => {
    await expect(page.getByRole("heading", { name: "Automation Workflows", exact: true })).toBeVisible({ timeout: 10000 });
    await expect(page.getByRole("link", { name: "Workflows", exact: true })).toBeVisible();
    await expect(page.getByRole("link", { name: "Drafts", exact: true })).toBeVisible();
    await expect(page.getByRole("link", { name: "Templates", exact: true })).toBeVisible();
    await expect(page.getByRole("link", { name: "Settings", exact: true })).toBeVisible();
  });

  test("can create, edit, trigger, and delete an automation workflow", async ({ page }) => {
    // 1. Open Create Modal
    await page.getByRole("button", { name: /New Workflow|Create First Workflow/i }).first().click();
    await expect(page.getByRole("heading", { name: "Create Automation Workflow" })).toBeVisible();

    // 2. Fill form
    const uniqueTitle = `Workflow E2E ${Date.now()}`;
    await page.getByPlaceholder(/e\.g\. Daily Tech Highlights/i).fill(uniqueTitle);
    await page.getByPlaceholder(/e\.g\. Draft a 3-part thread/i).fill("Extract 3 actionable tips.");

    // Submit
    await page.getByRole("button", { name: "Create Workflow" }).click();

    // 3. Verify card in dashboard
    await expect(page.getByText(uniqueTitle)).toBeVisible({ timeout: 10000 });
    await expect(page.getByText("Extract 3 actionable tips.")).toBeVisible();

    // 4. Edit Workflow
    await page.getByRole("button", { name: "Edit" }).first().click();
    await expect(page.getByRole("heading", { name: "Edit Automation Workflow" })).toBeVisible();
    const updatedTitle = `${uniqueTitle} - Edited`;
    await page.getByPlaceholder(/e\.g\. Daily Tech Highlights/i).fill(updatedTitle);
    await page.getByRole("button", { name: "Save Changes" }).click();

    await expect(page.getByText(updatedTitle)).toBeVisible({ timeout: 10000 });

    // 5. Trigger Workflow
    await page.getByRole("button", { name: /Trigger/i }).first().click();

    // 6. Navigation to Drafts and back to Workflows
    await page.getByRole("link", { name: "Drafts", exact: true }).click();
    await expect(page.getByRole("heading", { name: "Drafts Library", exact: true })).toBeVisible({ timeout: 10000 });

    await page.getByRole("link", { name: "Workflows", exact: true }).click();
    await expect(page.getByRole("heading", { name: "Automation Workflows", exact: true })).toBeVisible({ timeout: 10000 });

    // 7. Delete Workflow
    page.once("dialog", (dialog) => dialog.accept());
    await page.getByRole("button", { name: "Delete workflow" }).first().click();

    await expect(page.getByText(updatedTitle)).not.toBeVisible({ timeout: 10000 });
  });
});
