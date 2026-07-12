import { expect, test } from "@playwright/test";
import { loginViaUi } from "../helpers/auth.js";

test.describe("Chat", () => {
  test.beforeEach(async ({ page }) => {
    await loginViaUi(page);
  });

  test("can send a message and receive a response", async ({ page }) => {
    const input = page.getByPlaceholder("Ask the assistant...");
    await input.fill("Hello, can you help me with my draft?");
    await page.getByRole("button", { name: "Send" }).click();

    await expect(page.getByText("assistant").first()).toBeVisible({ timeout: 10000 });
    await expect(page.getByText(/stage proposal/i).first()).toBeVisible({ timeout: 5000 });
  });
});