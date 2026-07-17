import { apiFetch } from "./client";

export interface FeedSubscriptionDto {
  id: string;
  title: string;
  feedUrl: string;
  websiteUrl?: string | null;
  instructionPrompt: string;
  autoPublish: boolean;
  lastPolledAt?: string | null;
  includeFilters?: string | null;
  excludeFilters?: string | null;
  createdAt: string;
}

export interface FeedDiscoveryResult {
  feedUrl: string;
  title?: string | null;
  websiteUrl?: string | null;
  success: boolean;
  error?: string | null;
}

export interface FeedQueueItemDto {
  id: string;
  feedSubscriptionId: string;
  feedSubscriptionTitle: string;
  itemTitle: string;
  itemLink: string;
  status: "Pending" | "Processing" | "Succeeded" | "Failed";
  attemptCount: number;
  maxAttempts: number;
  nextAttemptAt: string;
  lastError?: string | null;
  createdAt: string;
  updatedAt: string;
  completedAt?: string | null;
}

export async function discoverFeed(url: string): Promise<FeedDiscoveryResult> {
  const res = await apiFetch("/api/feeds/discover", {
    method: "POST",
    body: JSON.stringify({ url }),
  });
  if (!res.ok) {
    const msg = await res.text().catch(() => "");
    throw new Error(msg || `discoverFeed failed: ${res.status}`);
  }
  return res.json();
}

export async function fetchFeeds(): Promise<FeedSubscriptionDto[]> {
  const res = await apiFetch("/api/feeds");
  if (!res.ok) throw new Error(`fetchFeeds failed: ${res.status}`);
  return res.json();
}

export async function createFeed(sub: {
  title: string;
  feedUrl: string;
  websiteUrl?: string | null;
  instructionPrompt: string;
  autoPublish: boolean;
  includeFilters?: string | null;
  excludeFilters?: string | null;
}): Promise<FeedSubscriptionDto> {
  const res = await apiFetch("/api/feeds", {
    method: "POST",
    body: JSON.stringify(sub),
  });
  if (!res.ok) {
    const msg = await res.text().catch(() => "");
    throw new Error(msg || `createFeed failed: ${res.status}`);
  }
  return res.json();
}

export async function updateFeed(
  id: string,
  sub: {
    title: string;
    feedUrl: string;
    websiteUrl?: string | null;
    instructionPrompt: string;
    autoPublish: boolean;
    includeFilters?: string | null;
    excludeFilters?: string | null;
  }
): Promise<FeedSubscriptionDto> {
  const res = await apiFetch(`/api/feeds/${id}`, {
    method: "PUT",
    body: JSON.stringify(sub),
  });
  if (!res.ok) {
    const msg = await res.text().catch(() => "");
    throw new Error(msg || `updateFeed failed: ${res.status}`);
  }
  return res.json();
}

export async function deleteFeed(id: string): Promise<void> {
  const res = await apiFetch(`/api/feeds/${id}`, {
    method: "DELETE",
  });
  if (!res.ok) throw new Error(`deleteFeed failed: ${res.status}`);
}

export async function triggerFeed(id: string): Promise<void> {
  const res = await apiFetch(`/api/feeds/${id}/trigger`, {
    method: "POST",
  });
  if (!res.ok) {
    const msg = await res.text().catch(() => "");
    throw new Error(msg || `triggerFeed failed: ${res.status}`);
  }
}

export async function fetchFeedQueue(): Promise<FeedQueueItemDto[]> {
  const res = await apiFetch("/api/feeds/queue");
  if (!res.ok) throw new Error(`fetchFeedQueue failed: ${res.status}`);
  return res.json();
}

export async function retryFeedQueueItem(id: string): Promise<void> {
  const res = await apiFetch(`/api/feeds/queue/${id}/retry`, {
    method: "POST",
  });
  if (!res.ok) {
    const msg = await res.text().catch(() => "");
    throw new Error(msg || `retryFeedQueueItem failed: ${res.status}`);
  }
}

export async function deleteFeedQueueItem(id: string): Promise<void> {
  const res = await apiFetch(`/api/feeds/queue/${id}`, {
    method: "DELETE",
  });
  if (!res.ok) {
    const msg = await res.text().catch(() => "");
    throw new Error(msg || `deleteFeedQueueItem failed: ${res.status}`);
  }
}
