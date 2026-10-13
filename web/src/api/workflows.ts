import { apiFetch } from "./client";
import type { FeedQueueItemDto } from "./feeds";

export type WorkflowSourceKind = "Feed" | "WorkflowHighlights" | "Webhook" | "EmailInbox" | "Telegram";

export interface WorkflowSourceDto {
  id?: string;
  kind?: WorkflowSourceKind;
  feedSubscriptionId?: string | null;
  title?: string;
  feedUrl?: string | null;
  sourceWorkflowId?: string | null;
  sourceWorkflowTitle?: string | null;
  configurationJson?: string | null;
}

export interface WorkflowDestinationDto {
  id: string;
  platform: string;
  autoPublish: boolean;
  publishingMode?: "Manual" | "Automatic" | "Scheduled";
  scheduleId?: string | null;
  scheduleName?: string | null;
  accountId?: string | null;
  accountHandle?: string | null;
}

export interface AutomationWorkflowDto {
  id: string;
  title: string;
  instructionPrompt: string;
  sources: WorkflowSourceDto[];
  destinations: WorkflowDestinationDto[];
  scheduleId?: string | null;
  scheduleName?: string | null;
  minRelevanceScore?: number;
  createdAt: string;
  updatedAt: string;
}

export interface WorkflowDraftThreadDto {
  platform: string;
  stage: string;
  accountHandle?: string | null;
  variantId?: string | null;
}

export interface WorkflowDraftSummaryDto {
  id: string;
  title: string;
  status: string;
  targetPlatform?: string | null;
  threads: WorkflowDraftThreadDto[];
  discardReason?: string | null;
  relevanceScore?: number | null;
  content?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface CreateWorkflowDestinationRequest {
  platform: string;
  autoPublish?: boolean;
  publishingMode?: "Manual" | "Automatic" | "Scheduled";
  scheduleId?: string | null;
  accountId?: string | null;
}

export interface CreateWorkflowSourceRequest {
  kind: WorkflowSourceKind;
  feedSubscriptionId?: string | null;
  sourceWorkflowId?: string | null;
  configurationJson?: string | null;
}

export interface CreateAutomationWorkflowRequest {
  title: string;
  instructionPrompt?: string;
  feedSubscriptionIds?: string[];
  sources?: CreateWorkflowSourceRequest[];
  destinations?: CreateWorkflowDestinationRequest[];
  scheduleId?: string | null;
  minRelevanceScore?: number;
}

export async function fetchWorkflows(): Promise<AutomationWorkflowDto[]> {
  const res = await apiFetch("/api/workflows");
  if (!res.ok) throw new Error(`fetchWorkflows failed: ${res.status}`);
  return res.json();
}

export async function fetchWorkflow(id: string): Promise<AutomationWorkflowDto> {
  const res = await apiFetch(`/api/workflows/${id}`);
  if (!res.ok) throw new Error(`fetchWorkflow failed: ${res.status}`);
  return res.json();
}

export async function fetchWorkflowDrafts(workflowId: string): Promise<WorkflowDraftSummaryDto[]> {
  const res = await apiFetch(`/api/workflows/${workflowId}/drafts`);
  if (!res.ok) throw new Error(`fetchWorkflowDrafts failed: ${res.status}`);
  return res.json();
}

export async function fetchWorkflowQueue(workflowId: string): Promise<FeedQueueItemDto[]> {
  const res = await apiFetch(`/api/feeds/queue?workflowId=${workflowId}`);
  if (!res.ok) throw new Error(`fetchWorkflowQueue failed: ${res.status}`);
  return res.json();
}

export async function createWorkflow(req: CreateAutomationWorkflowRequest): Promise<AutomationWorkflowDto> {
  const res = await apiFetch("/api/workflows", {
    method: "POST",
    body: JSON.stringify(req),
  });
  if (!res.ok) {
    const msg = await res.text().catch(() => "");
    throw new Error(msg || `createWorkflow failed: ${res.status}`);
  }
  return res.json();
}

export async function updateWorkflow(id: string, req: CreateAutomationWorkflowRequest): Promise<AutomationWorkflowDto> {
  const res = await apiFetch(`/api/workflows/${id}`, {
    method: "PUT",
    body: JSON.stringify(req),
  });
  if (!res.ok) {
    const msg = await res.text().catch(() => "");
    throw new Error(msg || `updateWorkflow failed: ${res.status}`);
  }
  return res.json();
}

export async function deleteWorkflow(id: string): Promise<void> {
  const res = await apiFetch(`/api/workflows/${id}`, {
    method: "DELETE",
  });
  if (!res.ok) throw new Error(`deleteWorkflow failed: ${res.status}`);
}

export async function triggerWorkflow(id: string): Promise<void> {
  const res = await apiFetch(`/api/workflows/${id}/trigger`, {
    method: "POST",
  });
  if (!res.ok) {
    const msg = await res.text().catch(() => "");
    throw new Error(msg || `triggerWorkflow failed: ${res.status}`);
  }
}

export async function retryWorkflowFailedDrafts(id: string): Promise<{ retriedCount: number; message: string }> {
  const res = await apiFetch(`/api/workflows/${id}/retry-failed`, {
    method: "POST",
  });
  if (!res.ok) {
    const msg = await res.text().catch(() => "");
    throw new Error(msg || `retryWorkflowFailedDrafts failed: ${res.status}`);
  }
  return res.json();
}

export async function retryWorkflowDraft(id: string, draftId: string): Promise<void> {
  const res = await apiFetch(`/api/workflows/${id}/drafts/${draftId}/retry`, {
    method: "POST",
  });
  if (!res.ok) {
    const msg = await res.text().catch(() => "");
    throw new Error(msg || `retryWorkflowDraft failed: ${res.status}`);
  }
}

