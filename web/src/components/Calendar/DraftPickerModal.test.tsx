import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { DraftPickerModal } from "./DraftPickerModal";
import * as draftsApi from "../../api/drafts";
import * as schedulesApi from "../../api/schedules";

vi.mock("../../api/drafts");
vi.mock("../../api/schedules");

const mockDrafts: draftsApi.DraftDto[] = [
  {
    id: "draft-1",
    title: "Awesome AI Thread",
    status: "Draft",
    content: "Content for thread",
    targetPlatform: "Bluesky",
    canonicalDraftId: null,
    threads: [
      {
        id: "thread-1",
        draftId: "draft-1",
        platform: "Bluesky",
        stage: "Draft",
        content: "Content",
        posts: []
      }
    ],
    createdAt: "2026-09-01T00:00:00Z",
    updatedAt: "2026-09-01T00:00:00Z"
  },
  {
    id: "draft-2",
    title: "Second Draft",
    status: "Draft",
    content: "Other content",
    targetPlatform: "Bluesky",
    canonicalDraftId: null,
    threads: [
      {
        id: "thread-2",
        draftId: "draft-2",
        platform: "Bluesky",
        stage: "Draft",
        content: "Other",
        posts: []
      }
    ],
    createdAt: "2026-09-01T00:00:00Z",
    updatedAt: "2026-09-01T00:00:00Z"
  }
];

describe("DraftPickerModal", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("loads available drafts and selects first by default", async () => {
    vi.mocked(draftsApi.fetchDrafts).mockResolvedValueOnce(mockDrafts);

    render(
      <DraftPickerModal
        isOpen={true}
        onClose={vi.fn()}
        scheduleId="sched-1"
        onDraftQueued={vi.fn()}
      />
    );

    await waitFor(() => {
      expect(screen.getByText("Queue Draft to Schedule")).toBeInTheDocument();
      expect(screen.getByText("Awesome AI Thread")).toBeInTheDocument();
      expect(screen.getByText("Second Draft")).toBeInTheDocument();
    });
  });

  it("filters drafts by search input", async () => {
    vi.mocked(draftsApi.fetchDrafts).mockResolvedValueOnce(mockDrafts);

    render(
      <DraftPickerModal
        isOpen={true}
        onClose={vi.fn()}
        scheduleId="sched-1"
        onDraftQueued={vi.fn()}
      />
    );

    await waitFor(() => {
      expect(screen.getByText("Awesome AI Thread")).toBeInTheDocument();
    });

    const searchInput = screen.getByTestId("draft-search-input");
    fireEvent.change(searchInput, { target: { value: "Second" } });

    expect(screen.queryByText("Awesome AI Thread")).not.toBeInTheDocument();
    expect(screen.getByText("Second Draft")).toBeInTheDocument();
  });

  it("queuing draft invokes enqueueQueueItem and onDraftQueued", async () => {
    vi.mocked(draftsApi.fetchDrafts).mockResolvedValueOnce(mockDrafts);
    vi.mocked(schedulesApi.enqueueQueueItem).mockResolvedValueOnce({
      id: "q-1",
      scheduleId: "sched-1",
      draftId: "draft-1",
      draftTitle: "Awesome AI Thread",
      platformThreadId: "thread-1",
      platform: "Bluesky",
      position: 1,
      status: "Queued",
      createdAt: "2026-09-01T00:00:00Z"
    });

    const onQueued = vi.fn();
    const onClose = vi.fn();

    render(
      <DraftPickerModal
        isOpen={true}
        onClose={onClose}
        scheduleId="sched-1"
        onDraftQueued={onQueued}
      />
    );

    await waitFor(() => {
      expect(screen.getByText("Awesome AI Thread")).toBeInTheDocument();
    });

    const queueBtn = screen.getByTestId("confirm-queue-draft-btn");
    fireEvent.click(queueBtn);

    await waitFor(() => {
      expect(schedulesApi.enqueueQueueItem).toHaveBeenCalledWith("sched-1", {
        draftId: "draft-1",
        platformThreadId: "thread-1"
      });
      expect(onQueued).toHaveBeenCalled();
      expect(onClose).toHaveBeenCalled();
    });
  });
});
