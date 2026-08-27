import { expect, test } from "@playwright/test";
import { loginViaUi, navigateToComposer } from "../helpers/auth.js";

test.describe("Template Studio & Editor", () => {
  test.beforeEach(async ({ page }) => {
    await loginViaUi(page);
    await navigateToComposer(page);
  });

  test("can navigate to Template Studio and open visual editor", async ({ page }) => {
    await page.getByRole("link", { name: /templates/i }).click();
    await expect(page.getByRole("heading", { name: /template studio/i })).toBeVisible();

    await page.getByRole("button", { name: /new template/i }).click();
    await expect(page.getByTestId("template-editor-page")).toBeVisible();
    await expect(page.getByTestId("konva-container")).toBeVisible();

    await page.getByRole("button", { name: /show source/i }).click();
    await expect(page.getByText(/Template Source JSON/i)).toBeVisible();
  });

  test("can add image layer and configure fixed image / logo asset", async ({ page }) => {
    await page.getByRole("link", { name: /templates/i }).click();
    await page.getByRole("button", { name: /new template/i }).click();
    await expect(page.getByTestId("template-editor-page")).toBeVisible();

    await page.getByTestId("add-layer-image").click();
    await expect(page.getByRole("button", { name: /fixed asset \/ logo/i })).toBeVisible();

    const urlInput = page.getByPlaceholder(/or paste image url/i);
    await urlInput.fill("https://images.unsplash.com/photo-1579546929518-9e396f3cc809?w=400");
    await page.getByRole("button", { name: /^set$/i }).click();

    await expect(page.getByText(/attached/i)).toBeVisible();
  });
});
