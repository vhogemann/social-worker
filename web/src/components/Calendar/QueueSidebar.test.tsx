import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { QueueSidebar } from "./QueueSidebar";
import * as schedulesApi from "../../api/schedules";

vi.mock("../../api/schedules");

const mockQueueItems: schedulesApi.ScheduleQueueItemDto[] = [
  {
    id: "item-1",
    scheduleId: "sched-1",
    draftId: "draft-1",
    draftTitle: "First Queued Post",
    platformThreadId: "th-1",
    platform: "Bluesky",
    position: 1,
    status: "Queued",
    createdAt: "2026-09-01T00:00:00Z"
  },
  {
    id: "item-2",
    scheduleId: "sched-1",
    draftId: "draft-2",
    draftTitle: "Second Queued Post",
    platformThreadId: "th-2",
    platform: "Bluesky",
    position: 2,
    status: "Queued",
    createdAt: "2026-09-01T00:00:00Z"
  }
];

describe("QueueSidebar", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders queue items in position order", async () => {
    vi.mocked(schedulesApi.fetchScheduleQueue).mockResolvedValueOnce(mockQueueItems);

    render(
      <QueueSidebar
        scheduleId="sched-1"
        scheduleName="Morning Beat"
        onQueueUpdated={vi.fn()}
        onOpenDraftPicker={vi.fn()}
      />
    );

    await waitFor(() => {
      expect(screen.getByText("Input Queue")).toBeInTheDocument();
      expect(screen.getByText("First Queued Post")).toBeInTheDocument();
      expect(screen.getByText("Second Queued Post")).toBeInTheDocument();
      expect(screen.getByText("#1")).toBeInTheDocument();
      expect(screen.getByText("#2")).toBeInTheDocument();
    });
  });

  it("moving item down reorders queue", async () => {
    vi.mocked(schedulesApi.fetchScheduleQueue).mockResolvedValue(mockQueueItems);
    vi.mocked(schedulesApi.reorderQueue).mockResolvedValueOnce([
      mockQueueItems[1],
      mockQueueItems[0]
    ]);

    const onQueueUpdated = vi.fn();

    render(
      <QueueSidebar
        scheduleId="sched-1"
        scheduleName="Morning Beat"
        onQueueUpdated={onQueueUpdated}
        onOpenDraftPicker={vi.fn()}
      />
    );

    await waitFor(() => {
      expect(screen.getByTestId("move-down-item-1")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId("move-down-item-1"));

    await waitFor(() => {
      expect(schedulesApi.reorderQueue).toHaveBeenCalledWith("sched-1", ["item-2", "item-1"]);
      expect(onQueueUpdated).toHaveBeenCalled();
    });
  });

  it("toggling pause calls pauseQueueItem", async () => {
    vi.mocked(schedulesApi.fetchScheduleQueue).mockResolvedValue(mockQueueItems);
    vi.mocked(schedulesApi.pauseQueueItem).mockResolvedValueOnce({
      ...mockQueueItems[0],
      status: "Paused"
    });

    const onQueueUpdated = vi.fn();

    render(
      <QueueSidebar
        scheduleId="sched-1"
        scheduleName="Morning Beat"
        onQueueUpdated={onQueueUpdated}
        onOpenDraftPicker={vi.fn()}
      />
    );

    await waitFor(() => {
      expect(screen.getByTestId("pause-toggle-item-1")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId("pause-toggle-item-1"));

    await waitFor(() => {
      expect(schedulesApi.pauseQueueItem).toHaveBeenCalledWith("sched-1", "item-1", true);
      expect(onQueueUpdated).toHaveBeenCalled();
    });
  });

  it("removing item calls removeQueueItem", async () => {
    vi.mocked(schedulesApi.fetchScheduleQueue).mockResolvedValue(mockQueueItems);
    vi.mocked(schedulesApi.removeQueueItem).mockResolvedValueOnce();

    const onQueueUpdated = vi.fn();

    render(
      <QueueSidebar
        scheduleId="sched-1"
        scheduleName="Morning Beat"
        onQueueUpdated={onQueueUpdated}
        onOpenDraftPicker={vi.fn()}
      />
    );

    await waitFor(() => {
      expect(screen.getByTestId("remove-item-item-1")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId("remove-item-item-1"));

    await waitFor(() => {
      expect(schedulesApi.removeQueueItem).toHaveBeenCalledWith("sched-1", "item-1");
      expect(onQueueUpdated).toHaveBeenCalled();
    });
  });
});
