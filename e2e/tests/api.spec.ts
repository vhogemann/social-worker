import { expect, test } from "@playwright/test";
import { apiFetchAsUser, loginAsAdmin, resetState } from "../helpers/api.js";

test("authenticated test reset clears drafts", async () => {
  const token = await loginAsAdmin();
  await resetState(token);

  const response = await apiFetchAsUser("/api/drafts", token);
  expect(response.ok).toBeTruthy();
  expect(await response.json()).toEqual([]);
});
