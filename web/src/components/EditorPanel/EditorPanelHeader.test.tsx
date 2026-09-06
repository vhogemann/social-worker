import React from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { EditorPanelHeader } from "./EditorPanelHeader";

describe("EditorPanelHeader", () => {
  it("renders editor label, mode switcher, adapt and publish buttons", () => {
    const onPublish = vi.fn();
    const onAdapt = vi.fn();
    render(
      <EditorPanelHeader
        targetPlatform="Bluesky"
        mode="edit"
        onChangeMode={vi.fn()}
        isCanonical={true}
        isSent={false}
        canPublish={true}
        isPublishing={false}
        onOpenAdaptModal={onAdapt}
        onPublish={onPublish}
      />
    );

    expect(screen.getByText("editor")).toBeInTheDocument();
    expect(screen.getByText("Bluesky")).toBeInTheDocument();

    const adaptBtn = screen.getByRole("button", { name: /adapt/i });
    expect(adaptBtn).toBeInTheDocument();
    fireEvent.click(adaptBtn);
    expect(onAdapt).toHaveBeenCalledTimes(1);

    const publishBtn = screen.getByTestId("publish-primary-btn");
    expect(publishBtn).toBeInTheDocument();
    fireEvent.click(publishBtn);
    expect(onPublish).toHaveBeenCalledTimes(1);
  });

  it("clicking split trigger opens menu and selecting schedule invokes onAddToSchedule", async () => {
    const onAddToSchedule = vi.fn();
    const mockSchedules = [
      {
        id: "sched-1",
        name: "Morning Beat",
        timezone: "UTC",
        isActive: true,
        slotRuleCount: 1,
        queueItemCount: 0,
        createdAt: "",
        updatedAt: ""
      }
    ];

    const schedulesApi = await import("../../api/schedules");
    vi.spyOn(schedulesApi, "fetchSchedules").mockResolvedValue(mockSchedules as any);

    render(
      <EditorPanelHeader
        targetPlatform="Bluesky"
        mode="edit"
        onChangeMode={vi.fn()}
        isCanonical={false}
        isSent={false}
        canPublish={true}
        isPublishing={false}
        onOpenAdaptModal={vi.fn()}
        onPublish={vi.fn()}
        onAddToSchedule={onAddToSchedule}
      />
    );

    const trigger = screen.getByTestId("publish-split-trigger");
    fireEvent.click(trigger);

    expect(screen.getByTestId("publish-split-menu")).toBeInTheDocument();
    expect(screen.getByText("Publish Now")).toBeInTheDocument();

    const scheduleOption = await screen.findByTestId("add-to-schedule-sched-1");
    fireEvent.click(scheduleOption);

    expect(onAddToSchedule).toHaveBeenCalledWith("sched-1");
  });

  it("renders scheduleStatus pill when draft is queued in a schedule", () => {
    render(
      <EditorPanelHeader
        targetPlatform="Bluesky"
        mode="edit"
        onChangeMode={vi.fn()}
        isCanonical={false}
        isSent={false}
        canPublish={true}
        isPublishing={false}
        onOpenAdaptModal={vi.fn()}
        onPublish={vi.fn()}
        scheduleStatus={{ scheduleName: "Morning Beat", position: 2 }}
      />
    );

    expect(screen.getByTestId("draft-schedule-status-pill")).toHaveTextContent(
      "In Schedule: Morning Beat (#2)"
    );
  });
});
