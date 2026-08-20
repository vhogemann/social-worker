const BASE_URL = process.env.BASE_URL ?? "http://web:80";
const API_PREFIX = `${BASE_URL}`;

export async function apiFetch(path: string, options?: RequestInit): Promise<Response> {
  const url = `${API_PREFIX}${path.startsWith("/") ? path : `/${path}`}`;
  return fetch(url, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...(options?.headers ?? {}),
    },
  });
}

export async function apiFetchAsUser(path: string, token: string, options?: RequestInit): Promise<Response> {
  return apiFetch(path, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
      ...(options?.headers ?? {}),
    },
  });
}

export async function loginAsAdmin(): Promise<string> {
  let lastError: any = null;
  for (let attempt = 0; attempt < 5; attempt++) {
    try {
      const res = await apiFetch("/api/auth/login", {
        method: "POST",
        body: JSON.stringify({
          emailOrUsername: process.env.ADMIN_USERNAME ?? "admin",
          password: process.env.ADMIN_PASSWORD ?? "changeme",
        }),
      });
      if (res.ok) {
        const body = await res.json();
        return body.accessToken;
      }
      lastError = new Error(`Login failed: ${res.status}`);
    } catch (err) {
      lastError = err;
    }
    await new Promise((r) => setTimeout(r, 1000));
  }
  throw lastError ?? new Error("Login failed after multiple attempts");
}

export async function resetState(token: string): Promise<void> {
  const res = await apiFetchAsUser("/api/__tests/reset", token, { method: "POST" });
  if (!res.ok) throw new Error(`Reset failed: ${res.status}`);
}