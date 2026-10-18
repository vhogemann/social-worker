import { apiFetch } from "./client";

export interface ReplyAgentWorkflowSummaryDto {
  id: string;
  title: string;
}

export interface ReplyAgentDto {
  id: string;
  userId: string;
  name: string;
  description: string | null;
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
  assignedWorkflowsCount: number;
  assignedPostsCount: number;
  workflows: ReplyAgentWorkflowSummaryDto[];
  createdAt: string;
  updatedAt: string;
}

export interface CreateReplyAgentRequest {
  name: string;
  description?: string | null;
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

export interface UpdateReplyAgentRequest {
  name: string;
  description?: string | null;
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

export interface AssignWorkflowReplyAgentRequest {
  replyAgentId: string | null;
}

export interface ReplyAgentAssignedPostDto {
  postId: string;
  replyAgentId: string;
  agentName: string;
  platform: string;
  remoteId: string | null;
  url: string | null;
  authorHandle: string | null;
  publishedText: string | null;
  isExternal: boolean;
  isActive: boolean;
  assignedAt: string;
  lastPolledAt: string | null;
  commentsAuditedCount: number;
  repliesSentCount: number;
}

export interface AssignReplyAgentPostRequest {
  postId?: string | null;
  postUrl?: string | null;
  platform?: string | null;
  remoteId?: string | null;
}

export interface WorkflowCommentAuditDto {
  id: string;
  replyAgentId: string;
  postId: string;
  platform: string;
  remoteCommentId: string;
  remoteParentId: string | null;
  remoteRootId: string | null;
  remoteAuthorHandle: string;
  remoteAuthorId: string;
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
  remoteLikeId?: string | null;
  publishedPostId: string | null;
  errorMessage: string | null;
  createdAt: string;
}

export async function listReplyAgents(): Promise<ReplyAgentDto[]> {
  const response = await apiFetch("/api/reply-agents");
  if (!response.ok) {
    throw new Error(`Failed to list reply agents: ${response.statusText}`);
  }
  return response.json();
}

export async function getReplyAgent(id: string): Promise<ReplyAgentDto> {
  const response = await apiFetch(`/api/reply-agents/${id}`);
  if (!response.ok) {
    throw new Error(`Failed to get reply agent: ${response.statusText}`);
  }
  return response.json();
}

export async function createReplyAgent(request: CreateReplyAgentRequest): Promise<ReplyAgentDto> {
  const response = await apiFetch("/api/reply-agents", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });
  if (!response.ok) {
    throw new Error(`Failed to create reply agent: ${response.statusText}`);
  }
  return response.json();
}

export async function updateReplyAgent(id: string, request: UpdateReplyAgentRequest): Promise<ReplyAgentDto> {
  const response = await apiFetch(`/api/reply-agents/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });
  if (!response.ok) {
    throw new Error(`Failed to update reply agent: ${response.statusText}`);
  }
  return response.json();
}

export async function deleteReplyAgent(id: string): Promise<void> {
  const response = await apiFetch(`/api/reply-agents/${id}`, {
    method: "DELETE",
  });
  if (!response.ok) {
    throw new Error(`Failed to delete reply agent: ${response.statusText}`);
  }
}

export async function getReplyAgentPosts(agentId: string): Promise<ReplyAgentAssignedPostDto[]> {
  const response = await apiFetch(`/api/reply-agents/${agentId}/posts`);
  if (!response.ok) {
    throw new Error(`Failed to list agent posts: ${response.statusText}`);
  }
  return response.json();
}

export async function getAllMonitoredPosts(agentId?: string): Promise<ReplyAgentAssignedPostDto[]> {
  const params = new URLSearchParams();
  if (agentId) params.set("agentId", agentId);
  const qs = params.toString() ? `?${params.toString()}` : "";
  const response = await apiFetch(`/api/reply-agents/posts${qs}`);
  if (!response.ok) {
    throw new Error(`Failed to list monitored posts: ${response.statusText}`);
  }
  return response.json();
}

export async function assignReplyAgentPost(
  agentId: string,
  request: AssignReplyAgentPostRequest
): Promise<ReplyAgentAssignedPostDto> {
  const response = await apiFetch(`/api/reply-agents/${agentId}/posts`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request),
  });
  if (!response.ok) {
    const errorData = await response.json().catch(() => null);
    throw new Error(errorData?.error || `Failed to assign post: ${response.statusText}`);
  }
  return response.json();
}

export async function unassignReplyAgentPost(agentId: string, postId: string): Promise<void> {
  const response = await apiFetch(`/api/reply-agents/${agentId}/posts/${postId}`, {
    method: "DELETE",
  });
  if (!response.ok) {
    throw new Error(`Failed to unassign post: ${response.statusText}`);
  }
}

export async function pollReplyAgentPost(agentId: string, postId: string): Promise<{ success: boolean }> {
  const response = await apiFetch(`/api/reply-agents/${agentId}/posts/${postId}/poll`, {
    method: "POST",
  });
  if (!response.ok) {
    throw new Error(`Failed to poll post: ${response.statusText}`);
  }
  return response.json();
}

export async function getReplyAgentAudits(params?: {
  agentId?: string;
  postId?: string;
  decision?: string;
  limit?: number;
  offset?: number;
}): Promise<WorkflowCommentAuditDto[]> {
  const query = new URLSearchParams();
  if (params?.agentId) query.set("agentId", params.agentId);
  if (params?.postId) query.set("postId", params.postId);
  if (params?.decision) query.set("decision", params.decision);
  if (params?.limit) query.set("limit", params.limit.toString());
  if (params?.offset) query.set("offset", params.offset.toString());
  const qs = query.toString() ? `?${query.toString()}` : "";

  const response = await apiFetch(`/api/reply-agents/audits${qs}`);
  if (!response.ok) {
    throw new Error(`Failed to fetch comment audits: ${response.statusText}`);
  }
  return response.json();
}

export async function approveReplyAgentAudit(
  auditId: string,
  replyText?: string
): Promise<{ success: boolean; remoteReplyId?: string; replyUrl?: string; replyText: string }> {
  const response = await apiFetch(`/api/reply-agents/audits/${auditId}/reply`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ replyText }),
  });
  if (!response.ok) {
    const errorData = await response.json().catch(() => null);
    throw new Error(errorData?.errorMessage || errorData?.error || `Failed to approve reply: ${response.statusText}`);
  }
  return response.json();
}

export async function getWorkflowAssignedReplyAgent(workflowId: string): Promise<ReplyAgentDto | null> {
  const response = await apiFetch(`/api/workflows/${workflowId}/reply-agent`);
  if (response.status === 404 || response.status === 204) {
    return null;
  }
  if (!response.ok) {
    throw new Error(`Failed to fetch workflow reply agent: ${response.statusText}`);
  }
  const text = await response.text();
  if (!text) {
    return null;
  }
  return JSON.parse(text);
}

export async function assignWorkflowReplyAgent(
  workflowId: string,
  replyAgentId: string | null
): Promise<ReplyAgentDto | null> {
  const response = await apiFetch(`/api/workflows/${workflowId}/reply-agent`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ replyAgentId }),
  });
  if (!response.ok) {
    throw new Error(`Failed to assign reply agent: ${response.statusText}`);
  }
  const text = await response.text();
  if (!text) {
    return null;
  }
  return JSON.parse(text);
}

export async function pollWorkflowReplyAgent(
  workflowId: string
): Promise<{ success: boolean; postsChecked: number }> {
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
  limit: number = 50,
  decision?: string
): Promise<WorkflowCommentAuditDto[]> {
  const params = new URLSearchParams();
  params.set("limit", limit.toString());
  if (decision) params.set("decision", decision);

  const response = await apiFetch(`/api/workflows/${workflowId}/reply-agent/audits?${params.toString()}`);
  if (!response.ok) {
    throw new Error(`Failed to fetch comment audits: ${response.statusText}`);
  }
  return response.json();
}

export async function approveWorkflowCommentReply(
  workflowId: string,
  auditId: string,
  replyText?: string
): Promise<{ success: boolean; remoteReplyId?: string; replyUrl?: string; replyText: string }> {
  const response = await apiFetch(`/api/workflows/${workflowId}/reply-agent/audits/${auditId}/reply`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ replyText }),
  });
  if (!response.ok) {
    const errorData = await response.json().catch(() => null);
    throw new Error(errorData?.errorMessage || errorData?.error || `Failed to approve and publish reply: ${response.statusText}`);
  }
  return response.json();
}
