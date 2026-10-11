import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import React from "react";
import { WorkflowReplyAgentTab } from "./WorkflowReplyAgentTab";
import * as replyAgentsApi from "../../api/replyAgents";

vi.mock("../../api/replyAgents");

describe("WorkflowReplyAgentTab", () => {
  const mockAgent: replyAgentsApi.WorkflowReplyAgentDto = {
    id: "agent-1",
    automationWorkflowId: "wf-1",
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
    createdAt: "2026-10-11T10:00:00Z",
    updatedAt: "2026-10-11T10:00:00Z"
  };

  const mockAudits: replyAgentsApi.WorkflowCommentAuditDto[] = [
    {
      id: "audit-1",
      workflowReplyAgentId: "agent-1",
      postId: "p-1",
      platform: "Bluesky",
      remoteCommentId: "c-1",
      remoteAuthorHandle: "alice.bsky.social",
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
      errorMessage: null,
      createdAt: "2026-10-11T12:01:00Z"
    }
  ];

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(replyAgentsApi.getWorkflowReplyAgent).mockResolvedValue(mockAgent);
    vi.mocked(replyAgentsApi.getWorkflowCommentAudits).mockResolvedValue(mockAudits);
  });

  it("renders agent configuration and audits", async () => {
    render(<WorkflowReplyAgentTab workflowId="wf-1" />);

    expect(screen.getByTestId("reply-agent-loading")).toBeInTheDocument();

    await waitFor(() => {
      expect(screen.getByTestId("workflow-reply-agent-tab")).toBeInTheDocument();
    });

    expect(screen.getByText("Reply Agent")).toBeInTheDocument();
    expect(screen.getByText("Active")).toBeInTheDocument();
    expect(screen.getByText("Auto-Publish")).toBeInTheDocument();

    const personaInput = screen.getByTestId("agent-persona-input") as HTMLTextAreaElement;
    expect(personaInput.value).toBe("Polite assistant persona");

    expect(screen.getByText("@alice.bsky.social")).toBeInTheDocument();
    expect(screen.getByText('"Can you clarify this detail?"')).toBeInTheDocument();
    expect(screen.getByText("Score: 8/10")).toBeInTheDocument();
    expect(screen.getByText("Replied")).toBeInTheDocument();
    expect(screen.getByText("Certainly! The detail means X.")).toBeInTheDocument();
  });

  it("submits updated configuration when save button is clicked", async () => {
    vi.mocked(replyAgentsApi.updateWorkflowReplyAgent).mockResolvedValue({
      ...mockAgent,
      minRankingScore: 9,
      personaPrompt: "Updated instructions"
    });

    render(<WorkflowReplyAgentTab workflowId="wf-1" />);

    await waitFor(() => {
      expect(screen.getByTestId("workflow-reply-agent-tab")).toBeInTheDocument();
    });

    const personaInput = screen.getByTestId("agent-persona-input");
    fireEvent.change(personaInput, { target: { value: "Updated instructions" } });

    const minScoreInput = screen.getByTestId("min-score-input");
    fireEvent.change(minScoreInput, { target: { value: "9" } });

    fireEvent.click(screen.getByTestId("save-agent-settings-button"));

    await waitFor(() => {
      expect(replyAgentsApi.updateWorkflowReplyAgent).toHaveBeenCalledWith("wf-1", expect.objectContaining({
        personaPrompt: "Updated instructions",
        minRankingScore: 9
      }));
    });

    expect(screen.getByText("Reply Agent settings saved successfully.")).toBeInTheDocument();
  });

  it("triggers poll when poll button is clicked", async () => {
    vi.mocked(replyAgentsApi.pollWorkflowReplyAgent).mockResolvedValue({
      success: true,
      message: "Check scheduled."
    });

    render(<WorkflowReplyAgentTab workflowId="wf-1" />);

    await waitFor(() => {
      expect(screen.getByTestId("workflow-reply-agent-tab")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId("poll-replies-button"));

    await waitFor(() => {
      expect(replyAgentsApi.pollWorkflowReplyAgent).toHaveBeenCalledWith("wf-1");
    });

    expect(screen.getByText("Check scheduled.")).toBeInTheDocument();
  });

  it("renders empty state when there are no evaluated comments", async () => {
    vi.mocked(replyAgentsApi.getWorkflowCommentAudits).mockResolvedValue([]);

    render(<WorkflowReplyAgentTab workflowId="wf-1" />);

    await waitFor(() => {
      expect(screen.getByTestId("workflow-reply-agent-tab")).toBeInTheDocument();
    });

    expect(screen.getByText("No comments evaluated yet")).toBeInTheDocument();
  });
});
