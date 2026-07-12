import { expect, test } from "@playwright/test";

 test("app loads and shows login", async ({ page }) => {
  await page.goto("/");

  await expect(page.getByRole("heading", { name: "social-worker" })).toBeVisible();
  await expect(page.getByPlaceholder("Email or username")).toBeVisible();
  await expect(page.getByRole("button", { name: "sign in" })).toBeVisible();
});

test("API responds", async ({ request }) => {
  const response = await request.post("/api/auth/login", {
    data: { emailOrUsername: "invalid", password: "invalid" },
  });

  expect(response.status()).toBe(401);
});
