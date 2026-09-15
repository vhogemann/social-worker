import { apiFetch } from "./client";

export interface UserDto {
  id: string;
  username: string;
  email: string;
  role: string;
  isActive?: boolean;
  preferredProviderId?: string | null;
}

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: UserDto;
}

export interface RefreshResponse {
  accessToken: string;
  expiresAt: string;
}

async function extractErrorMessage(res: Response, fallback: string): Promise<string> {
  try {
    const text = await res.text();
    if (!text) return fallback;
    try {
      const data = JSON.parse(text);
      if (typeof data === "string") return data;
      return data.error || data.message || fallback;
    } catch {
      return text;
    }
  } catch {
    return fallback;
  }
}

export async function login(emailOrUsername: string, password: string): Promise<LoginResponse> {
  const res = await fetch("/api/auth/login", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ emailOrUsername, password })
  });

  if (!res.ok) {
    const error = await extractErrorMessage(res, "Invalid credentials");
    throw new Error(error);
  }

  return res.json();
}

export async function refresh(refreshToken: string): Promise<RefreshResponse> {
  const res = await fetch("/api/auth/refresh", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ refreshToken })
  });

  if (!res.ok) {
    const error = await extractErrorMessage(res, "Session expired");
    throw new Error(error);
  }

  return res.json();
}

export async function logout(refreshToken: string): Promise<void> {
  await fetch("/api/auth/logout", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ refreshToken })
  });
}

export async function getMe(): Promise<UserDto> {
  const res = await apiFetch("/api/auth/me");
  if (!res.ok) {
    const error = await extractErrorMessage(res, "Unauthorized");
    throw new Error(error);
  }
  return res.json();
}

export async function changePassword(currentPassword: string, newPassword: string): Promise<void> {
  const res = await apiFetch("/api/account/password", {
    method: "PATCH",
    body: JSON.stringify({ currentPassword, newPassword })
  });

  if (!res.ok) {
    const error = await extractErrorMessage(res, "Failed to change password");
    throw new Error(error);
  }
}

export async function listUsers(): Promise<UserDto[]> {
  const res = await apiFetch("/api/users");
  if (!res.ok) {
    const error = await extractErrorMessage(res, "Failed to load users");
    throw new Error(error);
  }
  return res.json();
}

export async function createUser(req: Record<string, string>): Promise<UserDto> {
  const res = await apiFetch("/api/users", {
    method: "POST",
    body: JSON.stringify(req)
  });

  if (!res.ok) {
    const error = await extractErrorMessage(res, "Failed to create user");
    throw new Error(error);
  }

  return res.json();
}

export async function updateUser(id: string, req: Partial<Pick<UserDto, 'username' | 'email' | 'role' | 'isActive' | 'preferredProviderId'>>): Promise<UserDto> {
  const res = await apiFetch(`/api/users/${id}`, {
    method: "PATCH",
    body: JSON.stringify(req)
  });

  if (!res.ok) {
    const error = await extractErrorMessage(res, "Failed to update user");
    throw new Error(error);
  }

  return res.json();
}

export async function resetUserPassword(id: string, req: Record<string, string>): Promise<void> {
  const res = await apiFetch(`/api/users/${id}/password`, {
    method: "POST",
    body: JSON.stringify(req)
  });

  if (!res.ok) {
    const error = await extractErrorMessage(res, "Failed to reset password");
    throw new Error(error);
  }
}

export async function setPreferredProvider(providerId: string | null): Promise<void> {
  const res = await apiFetch("/api/account/provider", {
    method: "PATCH",
    body: JSON.stringify({ providerId })
  });

  if (!res.ok) {
    const error = await extractErrorMessage(res, "Failed to update preferred provider");
    throw new Error(error);
  }
}
