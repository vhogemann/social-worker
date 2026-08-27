import React from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { FeedQueueBacklog } from "./FeedQueueBacklog";
import { type FeedQueueItemDto } from "../../api/feeds";

const mockQueueItems: FeedQueueItemDto[] = [
  {
    id: "item-1",
    feedSubscriptionId: "feed-1",
    feedSubscriptionTitle: "Feed One",
    automationWorkflowId: "wf-1",
    automationWorkflowTitle: "Workflow One",
    itemTitle: "First Post",
    itemLink: "https://example.com/1",
    status: "Failed",
    attemptCount: 3,
    maxAttempts: 3,
    nextAttemptAt: "2026-08-01T12:00:00Z",
    lastError: "Connection timeout",
    createdAt: "2026-08-01T12:00:00Z",
    updatedAt: "2026-08-01T12:05:00Z"
  },
  {
    id: "item-2",
    feedSubscriptionId: "feed-1",
    feedSubscriptionTitle: "Feed One",
    automationWorkflowId: "wf-1",
    automationWorkflowTitle: "Workflow One",
    itemTitle: "Second Post",
    itemLink: "https://example.com/2",
    status: "Succeeded",
    attemptCount: 1,
    maxAttempts: 3,
    nextAttemptAt: "2026-08-01T12:00:00Z",
    createdAt: "2026-08-01T12:10:00Z",
    updatedAt: "2026-08-01T12:15:00Z"
  }
];

describe("FeedQueueBacklog", () => {
  it("renders queue items, filters, and triggers retry", () => {
    const handleRetry = vi.fn();
    const handleDelete = vi.fn();
    const handleRefresh = vi.fn();

    render(
      <FeedQueueBacklog
        items={mockQueueItems}
        isLoading={false}
        error={null}
        actionId={null}
        onRefresh={handleRefresh}
        onRetry={handleRetry}
        onDelete={handleDelete}
      />
    );

    expect(screen.getByText("Processing Backlog")).toBeInTheDocument();
    expect(screen.getByText("First Post")).toBeInTheDocument();
    expect(screen.getByText("Second Post")).toBeInTheDocument();

    const retryBtn = screen.getByRole("button", { name: /retry/i });
    fireEvent.click(retryBtn);
    expect(handleRetry).toHaveBeenCalledWith("item-1");

    const failedFilterBtn = screen.getByRole("button", { name: /^failed$/i });
    fireEvent.click(failedFilterBtn);

    expect(screen.getByText("First Post")).toBeInTheDocument();
    expect(screen.queryByText("Second Post")).not.toBeInTheDocument();
  });
});
