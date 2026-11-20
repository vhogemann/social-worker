import { apiFetch } from "./client";

export interface AccountDto {
  id: string;
  platform: string;
  handle: string;
  externalId?: string;
  tokenExpiresAt?: string;
  status: string;
  tags?: string[];
  createdAt: string;
  updatedAt: string;
}

export interface AccountRequest {
  platform: string;
  handle: string;
  appPassword?: string;
  tags?: string[];
}

export async function listAccounts(tag?: string): Promise<AccountDto[]> {
  const query = tag ? `?tag=${encodeURIComponent(tag)}` : "";
  const res = await apiFetch(`/api/accounts${query}`);
  if (!res.ok) throw new Error("Failed to fetch accounts");
  return res.json();
}

export async function updateAccountTags(
  id: string,
  req: { tags?: string[]; action?: string; add?: string[]; remove?: string[] }
): Promise<{ id: string; tags: string[] }> {
  const res = await apiFetch(`/api/accounts/${id}/tags`, {
    method: "PATCH",
    body: JSON.stringify(req),
  });
  if (!res.ok) throw new Error("Failed to update account tags");
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

export interface InstagramCallbackResponse {
  externalId: string;
  username: string;
  tokenExpiresAt: string | null;
}

export async function getInstagramAuthUrl(): Promise<{ authUrl: string }> {
  const res = await apiFetch("/api/accounts/instagram/auth-url");
  if (!res.ok) {
    const err = await res.json().catch(() => null);
    throw new Error(err?.error || err?.message || "Failed to fetch Instagram auth URL");
  }
  return res.json();
}

export async function submitInstagramCallback(code: string, state: string): Promise<InstagramCallbackResponse> {
  const res = await apiFetch("/api/accounts/instagram/callback", {
    method: "POST",
    body: JSON.stringify({ code, state }),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => null);
    throw new Error(err?.error || err?.message || "Failed to exchange Instagram auth code");
  }
  return res.json();
}

export interface MastodonCallbackResponse {
  externalId: string;
  handle: string;
  instanceUrl: string;
}

export async function getMastodonAuthUrl(instance: string): Promise<{ authUrl: string }> {
  const res = await apiFetch(`/api/accounts/mastodon/auth-url?instance=${encodeURIComponent(instance)}`);
  if (!res.ok) {
    const err = await res.json().catch(() => null);
    throw new Error(err?.error || err?.message || "Failed to fetch Mastodon auth URL");
  }
  return res.json();
}

export async function submitMastodonCallback(code: string, state: string): Promise<MastodonCallbackResponse> {
  const res = await apiFetch("/api/accounts/mastodon/callback", {
    method: "POST",
    body: JSON.stringify({ code, state }),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => null);
    throw new Error(err?.error || err?.message || "Failed to exchange Mastodon auth code");
  }
  return res.json();
}

export interface ConnectGitHubBlogRequest {
  owner: string;
  repo: string;
  accessToken: string;
  branch?: string;
  siteRootDirectory?: string;
  preset?: string;
  postsDirectory?: string;
  assetsDirectory?: string;
  siteBaseUrl?: string;
  permalinkFormat?: string;
  defaultLayout?: string;
  author?: string;
  defaultCategories?: string[];
  tags?: string[];
}

export interface GitHubBlogConnectionResponse {
  externalId: string;
  handle: string;
  branch: string;
  preset: string;
}

export async function connectGitHubBlog(req: ConnectGitHubBlogRequest): Promise<GitHubBlogConnectionResponse> {
  const res = await apiFetch("/api/accounts/github-blog", {
    method: "POST",
    body: JSON.stringify(req),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => null);
    throw new Error(err?.error || err?.message || "Failed to connect GitHub Blog repository");
  }
  return res.json();
}

export interface GitHubBlogSettingsDto {
  owner: string;
  repo: string;
  branch: string;
  preset: string;
  siteRootDirectory: string;
  postsDirectory: string;
  assetsDirectory: string;
  siteBaseUrl?: string | null;
  permalinkFormat: string;
  defaultLayout: string;
  author?: string | null;
  defaultCategories: string[];
}

export interface UpdateGitHubBlogSettingsRequest {
  owner?: string;
  repo?: string;
  accessToken?: string;
  branch?: string;
  preset?: string;
  siteRootDirectory?: string;
  postsDirectory?: string;
  assetsDirectory?: string;
  siteBaseUrl?: string;
  permalinkFormat?: string;
  defaultLayout?: string;
  author?: string;
  defaultCategories?: string[];
}

export async function getGitHubBlogSettings(accountId: string): Promise<GitHubBlogSettingsDto> {
  const res = await apiFetch(`/api/accounts/${accountId}/github-blog-settings`);
  if (!res.ok) {
    const err = await res.json().catch(() => null);
    throw new Error(err?.error || err?.message || "Failed to load GitHub Blog settings");
  }
  return res.json();
}

export async function updateGitHubBlogSettings(
  accountId: string,
  req: UpdateGitHubBlogSettingsRequest
): Promise<GitHubBlogSettingsDto> {
  const res = await apiFetch(`/api/accounts/${accountId}/github-blog-settings`, {
    method: "PUT",
    body: JSON.stringify(req),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => null);
    throw new Error(err?.error || err?.message || "Failed to update GitHub Blog settings");
  }
  return res.json();
}
