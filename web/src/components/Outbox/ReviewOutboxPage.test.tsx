import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { ReviewOutboxPage } from "./ReviewOutboxPage";
import * as outboxApi from "../../api/outbox";
import * as schedulesApi from "../../api/schedules";

vi.mock("../../api/outbox");
vi.mock("../../api/schedules");

const mockDrafts: outboxApi.OutboxDraftDto[] = [
  {
    draftId: "draft-1",
    title: "AI Breakthrough Post",
    content: "Exciting new AI developments announced today.",
    workflowId: "wf-1",
    workflowTitle: "Daily AI Digest",
    threads: [
      {
        threadId: "thread-1",
        platform: "Bluesky",
        stage: "Draft",
        content: "Exciting new AI developments announced today.",
        accountHandle: "test.bsky.social"
      }
    ],
    createdAt: "2026-09-07T08:00:00Z",
    updatedAt: "2026-09-07T08:00:00Z"
  }
];

const mockSchedules: schedulesApi.ScheduleSummaryDto[] = [
  {
    id: "sched-1",
    name: "Morning Schedule",
    timezone: "UTC",
    isActive: true,
    slotRuleCount: 1,
    queueItemCount: 0,
    createdAt: "2026-09-01T00:00:00Z",
    updatedAt: "2026-09-01T00:00:00Z"
  }
];

describe("ReviewOutboxPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders empty state when no outbox drafts are pending", async () => {
    vi.mocked(outboxApi.fetchOutboxDrafts).mockResolvedValueOnce([]);
    vi.mocked(schedulesApi.fetchSchedules).mockResolvedValueOnce(mockSchedules);

    render(
      <MemoryRouter>
        <ReviewOutboxPage />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText("Outbox is clear")).toBeInTheDocument();
    });
  });

  it("renders draft card with title, workflow title, and content", async () => {
    vi.mocked(outboxApi.fetchOutboxDrafts).mockResolvedValueOnce(mockDrafts);
    vi.mocked(schedulesApi.fetchSchedules).mockResolvedValueOnce(mockSchedules);

    render(
      <MemoryRouter>
        <ReviewOutboxPage />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText("AI Breakthrough Post")).toBeInTheDocument();
      expect(screen.getByText("Daily AI Digest")).toBeInTheDocument();
      expect(screen.getByText(/Exciting new AI developments/)).toBeInTheDocument();
    });
  });

  it("clicking Approve & Queue triggers approveOutboxDraft and removes card", async () => {
    vi.mocked(outboxApi.fetchOutboxDrafts).mockResolvedValueOnce(mockDrafts);
    vi.mocked(schedulesApi.fetchSchedules).mockResolvedValueOnce(mockSchedules);
    vi.mocked(outboxApi.approveOutboxDraft).mockResolvedValueOnce({ message: "Queued" });

    render(
      <MemoryRouter>
        <ReviewOutboxPage />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText("AI Breakthrough Post")).toBeInTheDocument();
    });

    const approveButton = screen.getByRole("button", { name: /Approve & Queue/i });
    fireEvent.click(approveButton);

    await waitFor(() => {
      expect(outboxApi.approveOutboxDraft).toHaveBeenCalledWith("draft-1", {
        action: "queue",
        scheduleId: "sched-1"
      });
      expect(screen.queryByText("AI Breakthrough Post")).not.toBeInTheDocument();
    });
  });

  it("clicking Publish Now triggers immediate publication and removes card", async () => {
    vi.mocked(outboxApi.fetchOutboxDrafts).mockResolvedValueOnce(mockDrafts);
    vi.mocked(schedulesApi.fetchSchedules).mockResolvedValueOnce(mockSchedules);
    vi.mocked(outboxApi.approveOutboxDraft).mockResolvedValueOnce({ message: "Published", postsCount: 1 });

    render(
      <MemoryRouter>
        <ReviewOutboxPage />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText("AI Breakthrough Post")).toBeInTheDocument();
    });

    const publishButton = screen.getByRole("button", { name: /Publish Now/i });
    fireEvent.click(publishButton);

    await waitFor(() => {
      expect(outboxApi.approveOutboxDraft).toHaveBeenCalledWith("draft-1", {
        action: "publish_now"
      });
      expect(screen.queryByText("AI Breakthrough Post")).not.toBeInTheDocument();
    });
  });

  it("dismissing draft confirms and calls dismissOutboxDraft", async () => {
    vi.mocked(outboxApi.fetchOutboxDrafts).mockResolvedValueOnce(mockDrafts);
    vi.mocked(schedulesApi.fetchSchedules).mockResolvedValueOnce(mockSchedules);
    vi.mocked(outboxApi.dismissOutboxDraft).mockResolvedValueOnce();

    render(
      <MemoryRouter>
        <ReviewOutboxPage />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText("AI Breakthrough Post")).toBeInTheDocument();
    });

    // Click trash button
    const card = screen.getByTestId("outbox-card-draft-1");
    const buttons = card.querySelectorAll("button");
    const trashButton = buttons[buttons.length - 1];
    fireEvent.click(trashButton);

    // Confirm modal opens
    expect(screen.getByText("Dismiss Outbox Draft")).toBeInTheDocument();
    const confirmButton = screen.getByRole("button", { name: "Dismiss Draft" });
    fireEvent.click(confirmButton);

    await waitFor(() => {
      expect(outboxApi.dismissOutboxDraft).toHaveBeenCalledWith("draft-1");
      expect(screen.queryByText("AI Breakthrough Post")).not.toBeInTheDocument();
    });
  });
});
