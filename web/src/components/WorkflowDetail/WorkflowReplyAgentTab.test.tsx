import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import React from "react";
import { MemoryRouter } from "react-router-dom";
import { WorkflowReplyAgentTab } from "./WorkflowReplyAgentTab";
import * as replyAgentsApi from "../../api/replyAgents";

vi.mock("../../api/replyAgents");

describe("WorkflowReplyAgentTab", () => {
  const mockAgent: replyAgentsApi.ReplyAgentDto = {
    id: "agent-1",
    userId: "user-1",
    name: "Tech Assistant",
    description: "Support agent",
    isEnabled: true,
    personaPrompt: "Polite assistant persona",
    minRankingScore: 7,
    maxCommentAgeHours: 24,
    maxPostAgeDays: 7,
    maxRepliesPerCommentThread: 2,
    maxRepliesPerUser: 3,
    maxThreadDepth: 4,
    autoPublishReplies: true,
    checkIntervalMinutes: 15,
    lastPolledAt: null,
    assignedWorkflowsCount: 1,
    assignedPostsCount: 0,
    workflows: [{ id: "wf-1", title: "Tech Feed" }],
    createdAt: "2026-10-11T10:00:00Z",
    updatedAt: "2026-10-11T10:00:00Z"
  };

  const mockAudits: replyAgentsApi.WorkflowCommentAuditDto[] = [
    {
      id: "audit-1",
      replyAgentId: "agent-1",
      postId: "p-1",
      platform: "Bluesky",
      remoteCommentId: "c-1",
      remoteParentId: null,
      remoteRootId: null,
      remoteAuthorHandle: "alice.bsky.social",
      remoteAuthorId: "did:plc:alice",
      commentText: "Can you clarify this detail?",
      parsedLinksJson: JSON.stringify([{ url: "https://example.com", title: "Example" }]),
      parsedImagesJson: null,
      commentCreatedAt: "2026-10-11T12:00:00Z",
      rankingScore: 8,
      rankingCategory: "GenuineQuestion",
      rankingDecision: "Replied",
      rankingReasoning: "Good question needing clarification.",
      generatedReplyText: "Certainly! The detail means X.",
      remoteReplyId: "reply-123",
      publishedPostId: null,
      errorMessage: null,
      createdAt: "2026-10-11T12:01:00Z"
    }
  ];

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(replyAgentsApi.getWorkflowAssignedReplyAgent).mockResolvedValue(mockAgent);
    vi.mocked(replyAgentsApi.listReplyAgents).mockResolvedValue([mockAgent]);
    vi.mocked(replyAgentsApi.getWorkflowCommentAudits).mockResolvedValue(mockAudits);
  });

  it("renders assigned agent and audits", async () => {
    render(
      <MemoryRouter>
        <WorkflowReplyAgentTab workflowId="wf-1" />
      </MemoryRouter>
    );

    expect(screen.getByTestId("reply-agent-loading")).toBeInTheDocument();

    await waitFor(() => {
      expect(screen.getByTestId("workflow-reply-agent-tab")).toBeInTheDocument();
    });

    expect(screen.getByText("Assigned Reply Agent")).toBeInTheDocument();
    expect(screen.getByText("Active")).toBeInTheDocument();
    expect(screen.getByText("Tech Assistant")).toBeInTheDocument();

    expect(screen.getByText("@alice.bsky.social")).toBeInTheDocument();
    expect(screen.getByText('"Can you clarify this detail?"')).toBeInTheDocument();
    expect(screen.getByText("Score: 8/10")).toBeInTheDocument();
    expect(screen.getByText("Replied")).toBeInTheDocument();
    expect(screen.getByText("Certainly! The detail means X.")).toBeInTheDocument();
  });

  it("renders correctly when workflow has no assigned reply agent", async () => {
    vi.mocked(replyAgentsApi.getWorkflowAssignedReplyAgent).mockResolvedValue(null);
    vi.mocked(replyAgentsApi.getWorkflowCommentAudits).mockResolvedValue([]);

    render(
      <MemoryRouter>
        <WorkflowReplyAgentTab workflowId="wf-no-agent" />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByTestId("workflow-reply-agent-tab")).toBeInTheDocument();
    });

    expect(screen.getByText("None Assigned")).toBeInTheDocument();
    expect(screen.getByTestId("agent-select-dropdown")).toHaveValue("");
  });

  it("assigns selected agent when apply button is clicked", async () => {
    const anotherAgent: replyAgentsApi.ReplyAgentDto = {
      ...mockAgent,
      id: "agent-2",
      name: "Community Bot",
    };
    vi.mocked(replyAgentsApi.listReplyAgents).mockResolvedValue([mockAgent, anotherAgent]);
    vi.mocked(replyAgentsApi.assignWorkflowReplyAgent).mockResolvedValue(anotherAgent);

    render(
      <MemoryRouter>
        <WorkflowReplyAgentTab workflowId="wf-1" />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByTestId("workflow-reply-agent-tab")).toBeInTheDocument();
    });

    const select = screen.getByTestId("agent-select-dropdown");
    fireEvent.change(select, { target: { value: "agent-2" } });

    const applyBtn = screen.getByTestId("save-assignment-button");
    fireEvent.click(applyBtn);

    await waitFor(() => {
      expect(replyAgentsApi.assignWorkflowReplyAgent).toHaveBeenCalledWith("wf-1", "agent-2");
    });

    expect(screen.getByText('Assigned "Community Bot" to this workflow.')).toBeInTheDocument();
  });

  it("triggers poll when poll button is clicked", async () => {
    vi.mocked(replyAgentsApi.pollWorkflowReplyAgent).mockResolvedValue({
      success: true,
      postsChecked: 3,
    });

    render(
      <MemoryRouter>
        <WorkflowReplyAgentTab workflowId="wf-1" />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByTestId("workflow-reply-agent-tab")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId("poll-replies-button"));

    await waitFor(() => {
      expect(replyAgentsApi.pollWorkflowReplyAgent).toHaveBeenCalledWith("wf-1");
    });

    expect(screen.getByText(/Reply monitoring check complete. Checked 3 post\(s\)./)).toBeInTheDocument();
  });

  it("renders empty state when there are no evaluated comments", async () => {
    vi.mocked(replyAgentsApi.getWorkflowCommentAudits).mockResolvedValue([]);

    render(
      <MemoryRouter>
        <WorkflowReplyAgentTab workflowId="wf-1" />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByTestId("workflow-reply-agent-tab")).toBeInTheDocument();
    });

    expect(screen.getByText("No comments evaluated yet")).toBeInTheDocument();
  });

  it("renders Approve & Reply button for PendingApproval audits and publishes reply", async () => {
    const pendingAudit: replyAgentsApi.WorkflowCommentAuditDto = {
      id: "audit-pending-1",
      replyAgentId: "agent-1",
      postId: "p-1",
      platform: "MetaThreads",
      remoteCommentId: "c-pending-1",
      remoteParentId: null,
      remoteRootId: null,
      remoteAuthorHandle: "wesley38_alb",
      remoteAuthorId: "did:threads:wesley",
      commentText: "Can you present the documents?",
      parsedLinksJson: null,
      parsedImagesJson: null,
      commentCreatedAt: "2026-10-11T12:00:00Z",
      rankingScore: 7,
      rankingCategory: "GenuineQuestion",
      rankingDecision: "PendingApproval",
      rankingReasoning: "Good question.",
      generatedReplyText: "Here are the documents from official records.",
      remoteReplyId: null,
      publishedPostId: null,
      errorMessage: null,
      createdAt: "2026-10-11T12:01:00Z"
    };

    vi.mocked(replyAgentsApi.getWorkflowCommentAudits).mockResolvedValue([pendingAudit]);
    vi.mocked(replyAgentsApi.approveWorkflowCommentReply).mockResolvedValue({
      success: true,
      remoteReplyId: "remote-reply-999",
      replyText: "Here are the documents from official records."
    });

    render(
      <MemoryRouter>
        <WorkflowReplyAgentTab workflowId="wf-1" />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByTestId("workflow-reply-agent-tab")).toBeInTheDocument();
    });

    expect(screen.getByText("Awaiting Review")).toBeInTheDocument();
    const approveBtn = screen.getByTestId("approve-reply-btn-audit-pending-1");
    expect(approveBtn).toBeInTheDocument();

    fireEvent.click(approveBtn);

    await waitFor(() => {
      expect(replyAgentsApi.approveWorkflowCommentReply).toHaveBeenCalledWith(
        "wf-1",
        "audit-pending-1",
        "Here are the documents from official records."
      );
    });

    expect(screen.getByText(/Reply published successfully!/)).toBeInTheDocument();
  });
});
