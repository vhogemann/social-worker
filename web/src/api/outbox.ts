import { apiFetch } from "./client";

export interface OutboxThreadDto {
  threadId: string;
  platform: string;
  stage: string;
  content?: string | null;
  accountId?: string | null;
  accountHandle?: string | null;
}

export interface OutboxDraftDto {
  draftId: string;
  title: string;
  content: string;
  workflowId?: string | null;
  workflowTitle?: string | null;
  threads: OutboxThreadDto[];
  createdAt: string;
  updatedAt: string;
}

export interface ApproveOutboxRequest {
  action: "queue" | "publish_now";
  scheduleId?: string | null;
  platformThreadId?: string | null;
}

export async function fetchOutboxDrafts(): Promise<OutboxDraftDto[]> {
  const res = await apiFetch("/api/outbox");
  if (!res.ok) throw new Error(`fetchOutboxDrafts failed: ${res.status}`);
  return res.json();
}

export async function approveOutboxDraft(
  draftId: string,
  req: ApproveOutboxRequest
): Promise<{ message: string; postsCount?: number }> {
  const res = await apiFetch(`/api/outbox/${draftId}/approve`, {
    method: "POST",
    body: JSON.stringify(req),
  });
  if (!res.ok) {
    const msg = await res.text().catch(() => "");
    throw new Error(msg || `approveOutboxDraft failed: ${res.status}`);
  }
  return res.json();
}

export async function dismissOutboxDraft(draftId: string): Promise<void> {
  const res = await apiFetch(`/api/outbox/${draftId}`, {
    method: "DELETE",
  });
  if (!res.ok) throw new Error(`dismissOutboxDraft failed: ${res.status}`);
}
