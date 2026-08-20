import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import React from "react";
import { MemoryRouter } from "react-router-dom";
import { LogsPanel } from "./LogsPanel";
import * as feedsApi from "../../api/feeds";

vi.mock("../../api/feeds", () => ({
  fetchFeedQueue: vi.fn(),
  retryFeedQueueItem: vi.fn(),
  deleteFeedQueueItem: vi.fn(),
}));

describe("LogsPanel", () => {
  const routerFuture = { v7_startTransition: true, v7_relativeSplatPath: true };

  beforeEach(() => {
    vi.clearAllMocks();

    vi.mocked(feedsApi.fetchFeedQueue).mockResolvedValue([
      {
        id: "log-1",
        feedSubscriptionId: "sub-1",
        feedSubscriptionTitle: "Tech News",
        automationWorkflowId: "wf-1",
        automationWorkflowTitle: "Bluesky Tech Digest",
        itemTitle: "Announcing .NET 10",
        itemLink: "https://example.com/net10",
        status: "Succeeded",
        attemptCount: 1,
        maxAttempts: 3,
        nextAttemptAt: "2026-08-20T12:00:00Z",
        lastError: null,
        createdAt: "2026-08-20T11:00:00Z",
        updatedAt: "2026-08-20T11:05:00Z",
        completedAt: "2026-08-20T11:05:00Z",
      },
      {
        id: "log-2",
        feedSubscriptionId: "sub-1",
        feedSubscriptionTitle: "Tech News",
        automationWorkflowId: "wf-1",
        automationWorkflowTitle: "Bluesky Tech Digest",
        itemTitle: "Failed Article Link",
        itemLink: "https://example.com/failed",
        status: "Failed",
        attemptCount: 3,
        maxAttempts: 3,
        nextAttemptAt: "2026-08-20T12:00:00Z",
        lastError: "Timeout while fetching",
        createdAt: "2026-08-20T11:00:00Z",
        updatedAt: "2026-08-20T11:15:00Z",
        completedAt: null,
      },
    ]);
  });

  it("renders log items and status badges", async () => {
    render(
      <MemoryRouter future={routerFuture}>
        <LogsPanel />
      </MemoryRouter>
    );

    expect(screen.getByText("Processing Logs & Backlog")).toBeInTheDocument();

    await waitFor(() => {
      expect(screen.getByText("Announcing .NET 10")).toBeInTheDocument();
      expect(screen.getByText("Failed Article Link")).toBeInTheDocument();
      expect(screen.getAllByText("Succeeded").length).toBeGreaterThan(0);
      expect(screen.getAllByText("Failed").length).toBeGreaterThan(0);
    });
  });

  it("filters items by status tab", async () => {
    render(
      <MemoryRouter future={routerFuture}>
        <LogsPanel />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText("Announcing .NET 10")).toBeInTheDocument();
    });

    const failedTab = screen.getByRole("button", { name: "Failed" });
    fireEvent.click(failedTab);

    expect(screen.getByText("Failed Article Link")).toBeInTheDocument();
    expect(screen.queryByText("Announcing .NET 10")).not.toBeInTheDocument();
  });
});
