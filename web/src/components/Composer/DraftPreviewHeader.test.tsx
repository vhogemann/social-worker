import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { DraftPreviewHeader } from "./DraftPreviewHeader";

describe("DraftPreviewHeader", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders hamburger button, draft title, platform badge and publish button", () => {
    const onOpenMenu = vi.fn();
    const onPublish = vi.fn();
    const onOpenAdapt = vi.fn();

    render(
      <DraftPreviewHeader
        title="My Epic Launch"
        targetPlatform="Bluesky"
        isCanonical={true}
        isSent={false}
        canPublish={true}
        isPublishing={false}
        onOpenMenu={onOpenMenu}
        onOpenAdaptModal={onOpenAdapt}
        onPublish={onPublish}
      />
    );

    expect(screen.getByText("My Epic Launch")).toBeInTheDocument();
    expect(screen.getByText("Bluesky")).toBeInTheDocument();

    const menuBtn = screen.getByTestId("draft-hamburger-btn");
    expect(menuBtn).toBeInTheDocument();
    fireEvent.click(menuBtn);
    expect(onOpenMenu).toHaveBeenCalledTimes(1);

    const publishBtn = screen.getByTestId("publish-primary-btn");
    expect(publishBtn).toBeInTheDocument();
    fireEvent.click(publishBtn);
    expect(onPublish).toHaveBeenCalledTimes(1);
  });

  it("renders published badge and link when isSent is true", () => {
    render(
      <DraftPreviewHeader
        title="My Epic Launch"
        targetPlatform="Bluesky"
        isCanonical={false}
        isSent={true}
        canPublish={false}
        isPublishing={false}
        onOpenMenu={vi.fn()}
        onOpenAdaptModal={vi.fn()}
        onPublish={vi.fn()}
        publishedUrl="https://bsky.app/profile/test/post/123"
      />
    );

    expect(screen.getByTestId("published-badge")).toBeInTheDocument();
    const link = screen.getByTestId("published-post-link");
    expect(link).toHaveAttribute("href", "https://bsky.app/profile/test/post/123");
  });

  it("opens schedule dropdown and selects schedule", async () => {
    const onAddToSchedule = vi.fn();
    const mockSchedules = [
      {
        id: "sched-1",
        name: "Morning Schedule",
        timezone: "UTC",
        isActive: true,
        slotRuleCount: 1,
        queueItemCount: 0,
        createdAt: "",
        updatedAt: "",
      },
    ];

    const schedulesApi = await import("../../api/schedules");
    vi.spyOn(schedulesApi, "fetchSchedules").mockResolvedValue(mockSchedules as any);

    render(
      <DraftPreviewHeader
        title="My Epic Launch"
        targetPlatform="Bluesky"
        isCanonical={false}
        isSent={false}
        canPublish={true}
        isPublishing={false}
        onOpenMenu={vi.fn()}
        onOpenAdaptModal={vi.fn()}
        onPublish={vi.fn()}
        onAddToSchedule={onAddToSchedule}
      />
    );

    const splitBtn = screen.getByTestId("publish-split-trigger");
    fireEvent.click(splitBtn);

    await waitFor(() => {
      expect(screen.getByTestId("add-to-schedule-sched-1")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId("add-to-schedule-sched-1"));
    expect(onAddToSchedule).toHaveBeenCalledWith("sched-1");
  });
});
