import { apiFetch } from "./client";
import type { FeedQueueItemDto } from "./feeds";

export interface WorkflowSourceDto {
  feedSubscriptionId: string;
  title: string;
  feedUrl: string;
}

export interface WorkflowDestinationDto {
  id: string;
  platform: string;
  autoPublish: boolean;
  accountId?: string | null;
  accountHandle?: string | null;
}

export interface AutomationWorkflowDto {
  id: string;
  title: string;
  instructionPrompt: string;
  sources: WorkflowSourceDto[];
  destinations: WorkflowDestinationDto[];
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
  createdAt: string;
  updatedAt: string;
}

export interface CreateWorkflowDestinationRequest {
  platform: string;
  autoPublish: boolean;
  accountId?: string | null;
}

export interface CreateAutomationWorkflowRequest {
  title: string;
  instructionPrompt?: string;
  feedSubscriptionIds?: string[];
  destinations?: CreateWorkflowDestinationRequest[];
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
