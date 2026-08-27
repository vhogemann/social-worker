import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import React from "react";
import { WorkflowExecutionHistoryModal } from "./WorkflowExecutionHistoryModal";
import * as workflowsApi from "../../api/workflows";
import * as feedsApi from "../../api/feeds";

vi.mock("../../api/workflows", () => ({
  fetchWorkflowQueue: vi.fn(),
}));

vi.mock("../../api/feeds", () => ({
  retryFeedQueueItem: vi.fn(),
  deleteFeedQueueItem: vi.fn(),
}));

describe("WorkflowExecutionHistoryModal", () => {
  const mockQueueItems: feedsApi.FeedQueueItemDto[] = [
    {
      id: "q-1",
      feedSubscriptionId: "sub-1",
      feedSubscriptionTitle: "Hacker News",
      automationWorkflowId: "wf-1",
      automationWorkflowTitle: "Tech News",
      itemTitle: "Article Succeeded",
      itemLink: "https://example.com/1",
      status: "Succeeded",
      attemptCount: 1,
      maxAttempts: 3,
      nextAttemptAt: "2026-08-20T10:00:00Z",
      createdAt: "2026-08-20T10:00:00Z",
      updatedAt: "2026-08-20T10:00:00Z",
    },
    {
      id: "q-2",
      feedSubscriptionId: "sub-1",
      feedSubscriptionTitle: "Hacker News",
      automationWorkflowId: "wf-1",
      automationWorkflowTitle: "Tech News",
      itemTitle: "Article Failed",
      itemLink: "https://example.com/2",
      status: "Failed",
      attemptCount: 3,
      maxAttempts: 3,
      nextAttemptAt: "2026-08-20T10:00:00Z",
      lastError: "LLM timeout",
      createdAt: "2026-08-20T10:00:00Z",
      updatedAt: "2026-08-20T10:00:00Z",
    }
  ];

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(workflowsApi.fetchWorkflowQueue).mockResolvedValue(mockQueueItems);
  });

  it("renders queue items and status badges when open", async () => {
    render(
      <WorkflowExecutionHistoryModal
        workflowId="wf-1"
        workflowTitle="Daily Tech"
        isOpen={true}
        onClose={vi.fn()}
      />
    );

    expect(screen.getByText("Execution History")).toBeInTheDocument();
    expect(screen.getByText("Daily Tech")).toBeInTheDocument();

    await waitFor(() => {
      expect(screen.getByText("Article Succeeded")).toBeInTheDocument();
      expect(screen.getByText("Article Failed")).toBeInTheDocument();
      expect(screen.getAllByText("Succeeded").length).toBeGreaterThan(0);
      expect(screen.getAllByText("Failed").length).toBeGreaterThan(0);
      expect(screen.getByText("Retry")).toBeInTheDocument();
    });
  });

  it("filters items by status tabs", async () => {
    render(
      <WorkflowExecutionHistoryModal
        workflowId="wf-1"
        workflowTitle="Daily Tech"
        isOpen={true}
        onClose={vi.fn()}
      />
    );

    await waitFor(() => {
      expect(screen.getByText("Article Succeeded")).toBeInTheDocument();
    });

    // Click 'Failed' filter tab
    const failedTab = screen.getByRole("button", { name: "Failed" });
    fireEvent.click(failedTab);

    expect(screen.queryByText("Article Succeeded")).not.toBeInTheDocument();
    expect(screen.getByText("Article Failed")).toBeInTheDocument();
  });

  it("calls retryFeedQueueItem when clicking Retry", async () => {
    vi.mocked(feedsApi.retryFeedQueueItem).mockResolvedValue();

    render(
      <WorkflowExecutionHistoryModal
        workflowId="wf-1"
        workflowTitle="Daily Tech"
        isOpen={true}
        onClose={vi.fn()}
      />
    );

    await waitFor(() => {
      expect(screen.getByText("Retry")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByText("Retry"));

    await waitFor(() => {
      expect(feedsApi.retryFeedQueueItem).toHaveBeenCalledWith("q-2");
    });
  });

  it("does not render when isOpen is false", () => {
    const { container } = render(
      <WorkflowExecutionHistoryModal
        workflowId="wf-1"
        workflowTitle="Daily Tech"
        isOpen={false}
        onClose={vi.fn()}
      />
    );

    expect(container.firstChild).toBeNull();
  });
});
