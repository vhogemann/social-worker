import { apiFetch } from "./client";

export interface WorkflowSourceDto {
  feedSubscriptionId: string;
  title: string;
  feedUrl: string;
}

export interface WorkflowDestinationDto {
  id: string;
  platform: string;
  autoPublish: boolean;
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

export interface CreateWorkflowDestinationRequest {
  platform: string;
  autoPublish: boolean;
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
