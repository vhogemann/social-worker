import { apiFetchAsUser } from "./api.js";

export async function cleanupTestData(token: string): Promise<void> {
  const res = await apiFetchAsUser("/api/__tests/reset", token, { method: "POST" });
  if (!res.ok) console.warn(`Cleanup warning: ${res.status}`);
}