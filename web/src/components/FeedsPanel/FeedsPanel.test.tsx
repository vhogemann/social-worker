import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { FeedsPanel } from "./FeedsPanel";
import * as feedsApi from "../../api/feeds";

vi.mock("../../api/feeds", () => ({
  fetchFeeds: vi.fn(),
  fetchFeedQueue: vi.fn(),
  createFeed: vi.fn(),
  updateFeed: vi.fn(),
  deleteFeed: vi.fn(),
  triggerFeed: vi.fn(),
  retryFeedQueueItem: vi.fn(),
  deleteFeedQueueItem: vi.fn(),
  discoverFeed: vi.fn()
}));

const mockFeeds: feedsApi.FeedSubscriptionDto[] = [
  {
    id: "feed-1",
    title: "Engineering Blog",
    feedUrl: "https://example.com/feed.xml",
    websiteUrl: "https://example.com",
    lastPolledAt: "2026-08-01T12:00:00Z",
    createdAt: "2026-08-01T10:00:00Z"
  }
];

const mockQueue: feedsApi.FeedQueueItemDto[] = [
  {
    id: "queue-1",
    feedSubscriptionId: "feed-1",
    feedSubscriptionTitle: "Engineering Blog",
    automationWorkflowId: "wf-1",
    automationWorkflowTitle: "Workflow One",
    itemTitle: "Announcing v1.0",
    itemLink: "https://example.com/posts/1",
    status: "Pending",
    attemptCount: 0,
    maxAttempts: 3,
    nextAttemptAt: "2026-08-01T12:00:00Z",
    createdAt: "2026-08-01T12:00:00Z",
    updatedAt: "2026-08-01T12:00:00Z"
  }
];

describe("FeedsPanel", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(feedsApi.fetchFeeds).mockResolvedValue(mockFeeds);
    vi.mocked(feedsApi.fetchFeedQueue).mockResolvedValue(mockQueue);
  });

  it("renders feeds and toggles the processing backlog", async () => {
    render(
      <MemoryRouter>
        <FeedsPanel />
      </MemoryRouter>
    );

    expect(await screen.findByText("Engineering Blog")).toBeInTheDocument();
    expect(screen.getByText("Processing Backlog (1)")).toBeInTheDocument();

    const backlogBtn = screen.getByRole("button", { name: /processing backlog/i });
    fireEvent.click(backlogBtn);

    expect(await screen.findByText("Announcing v1.0")).toBeInTheDocument();
  });

  it("triggers polling on a feed subscription", async () => {
    vi.mocked(feedsApi.triggerFeed).mockResolvedValue(undefined);

    render(
      <MemoryRouter>
        <FeedsPanel />
      </MemoryRouter>
    );

    expect(await screen.findByText("Engineering Blog")).toBeInTheDocument();
    const pollBtn = screen.getByRole("button", { name: /poll now/i });
    fireEvent.click(pollBtn);

    await waitFor(() => {
      expect(feedsApi.triggerFeed).toHaveBeenCalledWith("feed-1");
    });
  });
});
