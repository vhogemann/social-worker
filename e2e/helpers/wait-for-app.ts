const MAX_RETRIES = 30;
const POLL_INTERVAL_MS = 1000;

export async function waitForApp(baseUrl: string): Promise<void> {
  for (let i = 0; i < MAX_RETRIES; i++) {
    try {
      const res = await fetch(`${baseUrl}/api/auth/login`, { method: "POST" });
      if (res.status === 400 || res.status === 401) return;
    } catch {
      // not ready yet
    }
    await new Promise((r) => setTimeout(r, POLL_INTERVAL_MS));
  }
  throw new Error(`App did not become ready after ${MAX_RETRIES}s`);
}