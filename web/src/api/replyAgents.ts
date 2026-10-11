import { apiFetch } from "./client";

export interface WorkflowReplyAgentDto {
  id: string;
  automationWorkflowId: string;
  isEnabled: boolean;
  personaPrompt: string;
  maxCommentAgeHours: number;
  maxPostAgeDays: number;
  minRankingScore: number;
  maxRepliesPerCommentThread: number;
  maxRepliesPerUser: number;
  maxThreadDepth: number;
  autoPublishReplies: boolean;
  checkIntervalMinutes: number;
  lastPolledAt: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface UpdateWorkflowReplyAgentRequest {
  isEnabled?: boolean;
  personaPrompt?: string;
  maxCommentAgeHours?: number;
  maxPostAgeDays?: number;
  minRankingScore?: number;
  maxRepliesPerCommentThread?: number;
  maxRepliesPerUser?: number;
  maxThreadDepth?: number;
  autoPublishReplies?: boolean;
  checkIntervalMinutes?: number;
}

export interface WorkflowCommentAuditDto {
  id: string;
  workflowReplyAgentId: string;
  postId: string;
  platform: string;
  remoteCommentId: string;
  remoteAuthorHandle: string;
  commentText: string;
  parsedLinksJson: string | null;
  parsedImagesJson: string | null;
  commentCreatedAt: string;
  rankingScore: number;
  rankingCategory: string;
  rankingDecision: string;
  rankingReasoning: string | null;
  generatedReplyText: string | null;
  remoteReplyId: string | null;
  errorMessage: string | null;
  createdAt: string;
}

export async function getWorkflowReplyAgent(workflowId: string): Promise<WorkflowReplyAgentDto | null> {
  const response = await apiFetch(`/api/workflows/${workflowId}/reply-agent`);
  if (response.status === 404) {
    return null;
  }
  if (!response.ok) {
    throw new Error(`Failed to fetch workflow reply agent: ${response.statusText}`);
  }
  return response.json();
}

export async function updateWorkflowReplyAgent(
  workflowId: string,
  request: UpdateWorkflowReplyAgentRequest
): Promise<WorkflowReplyAgentDto> {
  const response = await apiFetch(`/api/workflows/${workflowId}/reply-agent`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });
  if (!response.ok) {
    throw new Error(`Failed to update workflow reply agent: ${response.statusText}`);
  }
  return response.json();
}

export async function pollWorkflowReplyAgent(
  workflowId: string
): Promise<{ success: boolean; message: string }> {
  const response = await apiFetch(`/api/workflows/${workflowId}/reply-agent/poll`, {
    method: "POST",
  });
  if (!response.ok) {
    throw new Error(`Failed to trigger reply agent poll: ${response.statusText}`);
  }
  return response.json();
}

export async function getWorkflowCommentAudits(
  workflowId: string,
  limit: number = 50
): Promise<WorkflowCommentAuditDto[]> {
  const response = await apiFetch(`/api/workflows/${workflowId}/reply-agent/audits?limit=${limit}`);
  if (!response.ok) {
    throw new Error(`Failed to fetch comment audits: ${response.statusText}`);
  }
  return response.json();
}
