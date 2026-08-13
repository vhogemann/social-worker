import { apiFetch } from "./client";

export interface AccountDto {
  id: string;
  platform: string;
  handle: string;
  status: string;
  createdAt: string;
  updatedAt: string;
}

export interface AccountRequest {
  platform: string;
  handle: string;
  appPassword?: string;
}

export async function listAccounts(): Promise<AccountDto[]> {
  const res = await apiFetch("/api/accounts");
  if (!res.ok) throw new Error("Failed to fetch accounts");
  return res.json();
}

export async function saveAccount(req: AccountRequest): Promise<void> {
  const res = await apiFetch("/api/accounts", {
    method: "POST",
    body: JSON.stringify(req),
  });
  if (!res.ok) throw new Error("Failed to save account");
}

export async function deleteAccount(id: string): Promise<void> {
  const res = await apiFetch(`/api/accounts/${id}`, {
    method: "DELETE",
  });
  if (!res.ok) throw new Error("Failed to delete account");
}

export async function getLinkedInAuthUrl(): Promise<{ authUrl: string }> {
  const res = await apiFetch("/api/accounts/linkedin/auth-url");
  if (!res.ok) {
    const err = await res.json().catch(() => null);
    throw new Error(err?.error || "Failed to fetch LinkedIn auth URL");
  }
  return res.json();
}

export async function submitLinkedInCallback(code: string): Promise<void> {
  const res = await apiFetch("/api/accounts/linkedin/callback", {
    method: "POST",
    body: JSON.stringify({ code }),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => null);
    throw new Error(err?.error || "Failed to exchange LinkedIn auth code");
  }
}

