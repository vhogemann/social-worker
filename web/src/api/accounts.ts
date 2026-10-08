import { apiFetch } from "./client";

export interface AccountDto {
  id: string;
  platform: string;
  handle: string;
  externalId?: string;
  tokenExpiresAt?: string;
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

export async function getLinkedInAuthUrl(type: "personal" | "company" = "personal"): Promise<{ authUrl: string; accountType?: string }> {
  const query = type ? `?type=${encodeURIComponent(type)}` : "";
  const res = await apiFetch(`/api/accounts/linkedin/auth-url${query}`);
  if (!res.ok) {
    const err = await res.json().catch(() => null);
    throw new Error(err?.error || "Failed to fetch LinkedIn auth URL");
  }
  return res.json();
}

export interface LinkedInCallbackResponse {
  personalHandle?: string | null;
  organizationHandles: string[];
}

export async function submitLinkedInCallback(code: string, state?: string, accountType?: string): Promise<LinkedInCallbackResponse> {
  const res = await apiFetch("/api/accounts/linkedin/callback", {
    method: "POST",
    body: JSON.stringify({ code, state, accountType }),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => null);
    throw new Error(err?.error || "Failed to exchange LinkedIn auth code");
  }
  return res.json();
}

export interface MetaThreadsCallbackResponse {
  externalId: string;
  username: string;
  tokenExpiresAt: string;
}

export async function getMetaThreadsAuthUrl(): Promise<{ authUrl: string }> {
  const res = await apiFetch("/api/accounts/metathreads/auth-url");
  if (!res.ok) {
    const err = await res.json().catch(() => null);
    throw new Error(err?.error || "Failed to fetch Threads auth URL");
  }
  return res.json();
}

export async function submitMetaThreadsCallback(code: string, state: string): Promise<MetaThreadsCallbackResponse> {
  const res = await apiFetch("/api/accounts/metathreads/callback", {
    method: "POST",
    body: JSON.stringify({ code, state }),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => null);
    throw new Error(err?.error || "Failed to exchange Threads auth code");
  }
  return res.json();
}

export interface TwitterCallbackResponse {
  externalId: string;
  username: string;
  tokenExpiresAt: string;
}

export async function getTwitterAuthUrl(): Promise<{ authUrl: string }> {
  const res = await apiFetch("/api/accounts/twitter/auth-url");
  if (!res.ok) {
    const err = await res.json().catch(() => null);
    throw new Error(err?.error || err?.message || "Failed to fetch Twitter auth URL");
  }
  return res.json();
}

export async function submitTwitterCallback(code: string, state: string): Promise<TwitterCallbackResponse> {
  const res = await apiFetch("/api/accounts/twitter/callback", {
    method: "POST",
    body: JSON.stringify({ code, state }),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => null);
    throw new Error(err?.error || err?.message || "Failed to exchange Twitter auth code");
  }
  return res.json();
}

