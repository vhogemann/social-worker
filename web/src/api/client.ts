import { useAuthStore } from "../store/authStore";

export async function apiFetch(url: string, options: RequestInit = {}): Promise<Response> {
  const { accessToken, refreshToken, setTokens, logout } = useAuthStore.getState();

  const headers = {
    ...options.headers,
  } as Record<string, string>;

  if (!headers["Content-Type"] && !(options.body instanceof FormData)) {
    headers["Content-Type"] = "application/json";
  }

  if (accessToken) {
    headers["Authorization"] = `Bearer ${accessToken}`;
  }

  options.headers = headers;
  let response = await fetch(url, options);

  if (response.status === 401 && refreshToken) {
    try {
      const refreshResponse = await fetch("/api/auth/refresh", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ refreshToken })
      });

      if (refreshResponse.ok) {
        const data = await refreshResponse.json();
        setTokens(data.accessToken, refreshToken);

        headers["Authorization"] = `Bearer ${data.accessToken}`;
        options.headers = headers;
        response = await fetch(url, options);
      } else {
        await logout();
      }
    } catch {
      await logout();
    }
  }

  return response;
}
