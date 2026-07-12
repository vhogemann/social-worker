const BASE_URL = process.env.BASE_URL ?? "http://web:80";
const API_PREFIX = `${BASE_URL}`;

export async function apiFetch(path: string, options?: RequestInit): Promise<Response> {
  const url = `${API_PREFIX}${path}`;
  const res = await fetch(url, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      "X-Demo-Mode": "true",
      ...(options?.headers ?? {}),
    },
  });
  return res;
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
  const res = await apiFetch("/api/auth/login", {
    method: "POST",
    body: JSON.stringify({
      emailOrUsername: process.env.ADMIN_USERNAME ?? "admin",
      password: process.env.ADMIN_PASSWORD ?? "changeme",
    }),
  });
  if (!res.ok) throw new Error(`Login failed: ${res.status}`);
  const body = await res.json();
  return body.accessToken;
}

export async function resetState(token: string): Promise<void> {
  const res = await apiFetchAsUser("/api/__tests/reset", token, { method: "POST" });
  if (!res.ok) throw new Error(`Reset failed: ${res.status}`);
}