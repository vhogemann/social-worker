import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { ScheduleManagerModal } from "./ScheduleManagerModal";
import * as schedulesApi from "../../api/schedules";

vi.mock("../../api/schedules");

const mockSchedules: schedulesApi.ScheduleSummaryDto[] = [
  {
    id: "sched-1",
    name: "Tech Beat",
    timezone: "Europe/London",
    isActive: true,
    slotRuleCount: 1,
    queueItemCount: 0,
    createdAt: "2026-09-01T00:00:00Z",
    updatedAt: "2026-09-01T00:00:00Z"
  }
];

const mockDetail: schedulesApi.ScheduleDetailDto = {
  id: "sched-1",
  name: "Tech Beat",
  timezone: "Europe/London",
  isActive: true,
  slotRules: [
    {
      id: "rule-1",
      scheduleId: "sched-1",
      recurrenceType: "Daily",
      cronExpression: "0 9 * * *",
      startTime: "09:00:00",
      endTime: "10:00:00",
      postCapacity: 4,
      createdAt: "2026-09-01T00:00:00Z"
    }
  ],
  queueItems: [],
  createdAt: "2026-09-01T00:00:00Z",
  updatedAt: "2026-09-01T00:00:00Z"
};

describe("ScheduleManagerModal", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("loads schedules and renders schedule settings", async () => {
    vi.mocked(schedulesApi.fetchSchedules).mockResolvedValueOnce(mockSchedules);
    vi.mocked(schedulesApi.fetchSchedule).mockResolvedValueOnce(mockDetail);

    render(
      <ScheduleManagerModal
        isOpen={true}
        onClose={vi.fn()}
        onSchedulesChanged={vi.fn()}
      />
    );

    await waitFor(() => {
      expect(screen.getByText("Calendar Settings")).toBeInTheDocument();
      expect(screen.getByDisplayValue("Tech Beat")).toBeInTheDocument();
      expect(screen.getByDisplayValue("Europe/London")).toBeInTheDocument();
    });
  });

  it("updates existing schedule", async () => {
    vi.mocked(schedulesApi.fetchSchedules).mockResolvedValue(mockSchedules);
    vi.mocked(schedulesApi.fetchSchedule).mockResolvedValue(mockDetail);
    vi.mocked(schedulesApi.updateSchedule).mockResolvedValueOnce(mockSchedules[0]);

    const onSchedulesChanged = vi.fn();

    render(
      <ScheduleManagerModal
        isOpen={true}
        onClose={vi.fn()}
        onSchedulesChanged={onSchedulesChanged}
      />
    );

    await waitFor(() => {
      expect(screen.getByDisplayValue("Tech Beat")).toBeInTheDocument();
    });

    const nameInput = screen.getByTestId("schedule-name-input");
    fireEvent.change(nameInput, { target: { value: "Updated Tech Beat" } });

    const saveBtn = screen.getByTestId("save-schedule-btn");
    fireEvent.click(saveBtn);

    await waitFor(() => {
      expect(schedulesApi.updateSchedule).toHaveBeenCalledWith("sched-1", {
        name: "Updated Tech Beat",
        timezone: "Europe/London",
        isActive: true
      });
      expect(onSchedulesChanged).toHaveBeenCalledWith("sched-1");
    });
  });

  it("switches to create mode and creates new schedule", async () => {
    vi.mocked(schedulesApi.fetchSchedules).mockResolvedValue(mockSchedules);
    vi.mocked(schedulesApi.fetchSchedule).mockResolvedValue(mockDetail);
    vi.mocked(schedulesApi.createSchedule).mockResolvedValueOnce({
      ...mockSchedules[0],
      id: "sched-new",
      name: "Brand New Beat"
    });

    const onSchedulesChanged = vi.fn();

    render(
      <ScheduleManagerModal
        isOpen={true}
        onClose={vi.fn()}
        onSchedulesChanged={onSchedulesChanged}
      />
    );

    await waitFor(() => {
      expect(screen.getByTestId("create-new-schedule-btn")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId("create-new-schedule-btn"));

    const nameInput = screen.getByTestId("schedule-name-input");
    fireEvent.change(nameInput, { target: { value: "Brand New Beat" } });

    const saveBtn = screen.getByTestId("save-schedule-btn");
    fireEvent.click(saveBtn);

    await waitFor(() => {
      expect(schedulesApi.createSchedule).toHaveBeenCalledWith(
        expect.objectContaining({
          name: "Brand New Beat"
        })
      );
      expect(onSchedulesChanged).toHaveBeenCalledWith("sched-new");
    });
  });
});
