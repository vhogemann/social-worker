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

export async function login(emailOrUsername: string, password: string): Promise<LoginResponse> {
  const res = await fetch("/api/auth/login", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ emailOrUsername, password })
  });

  if (!res.ok) {
    throw new Error("Invalid credentials");
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
    throw new Error("Session expired");
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
    throw new Error("Unauthorized");
  }
  return res.json();
}

export async function changePassword(currentPassword: string, newPassword: string): Promise<void> {
  const res = await apiFetch("/api/account/password", {
    method: "PATCH",
    body: JSON.stringify({ currentPassword, newPassword })
  });

  if (!res.ok) {
    const data = await res.json().catch(() => ({}));
    throw new Error(data.error || "Failed to change password");
  }
}

export async function listUsers(): Promise<UserDto[]> {
  const res = await apiFetch("/api/users");
  if (!res.ok) {
    throw new Error("Failed to load users");
  }
  return res.json();
}

export async function createUser(req: Record<string, string>): Promise<UserDto> {
  const res = await apiFetch("/api/users", {
    method: "POST",
    body: JSON.stringify(req)
  });

  if (!res.ok) {
    const data = await res.json().catch(() => ({}));
    throw new Error(data.error || "Failed to create user");
  }

  return res.json();
}

export async function updateUser(id: string, req: Partial<Pick<UserDto, 'username' | 'email' | 'role' | 'isActive' | 'preferredProviderId'>>): Promise<UserDto> {
  const res = await apiFetch(`/api/users/${id}`, {
    method: "PATCH",
    body: JSON.stringify(req)
  });

  if (!res.ok) {
    throw new Error("Failed to update user");
  }

  return res.json();
}

export async function resetUserPassword(id: string, req: Record<string, string>): Promise<void> {
  const res = await apiFetch(`/api/users/${id}/password`, {
    method: "POST",
    body: JSON.stringify(req)
  });

  if (!res.ok) {
    throw new Error("Failed to reset password");
  }
}

export async function setPreferredProvider(providerId: string | null): Promise<void> {
  const res = await apiFetch("/api/account/provider", {
    method: "PATCH",
    body: JSON.stringify({ providerId })
  });

  if (!res.ok) {
    throw new Error("Failed to update preferred provider");
  }
}
