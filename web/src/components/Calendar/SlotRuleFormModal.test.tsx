import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { SlotRuleFormModal, calculatePacingPreview } from "./SlotRuleFormModal";
import * as schedulesApi from "../../api/schedules";

vi.mock("../../api/schedules");

describe("SlotRuleFormModal", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("calculatePacingPreview computes equal intervals accurately", () => {
    // 09:00 to 10:00 (60 mins) with capacity 4 -> every 15 mins
    const res4 = calculatePacingPreview("09:00", "10:00", 4);
    expect(res4.isValid).toBe(true);
    expect(res4.message).toContain("every 15 minutes");
    expect(res4.sampleTimes).toEqual(["09:00", "09:15", "09:30", "09:45"]);

    // Capacity 1 -> single post
    const res1 = calculatePacingPreview("09:00", "10:00", 1);
    expect(res1.isValid).toBe(true);
    expect(res1.message).toBe("Publishes 1 post at 09:00");
    expect(res1.sampleTimes).toEqual(["09:00"]);

    // End time before start time -> invalid
    const invalid = calculatePacingPreview("11:00", "10:00", 2);
    expect(invalid.isValid).toBe(false);
    expect(invalid.message).toContain("End time must be strictly later");
  });

  it("renders modal with daily recurrence by default and displays live preview", () => {
    render(
      <SlotRuleFormModal
        isOpen={true}
        onClose={vi.fn()}
        scheduleId="sched-1"
        onCreated={vi.fn()}
      />
    );

    expect(screen.getByText("Add Publishing Slot Rule")).toBeInTheDocument();
    expect(screen.getByTestId("pacing-preview")).toHaveTextContent("every 15 minutes");
  });

  it("changing recurrence to weekly reveals day toggles", () => {
    render(
      <SlotRuleFormModal
        isOpen={true}
        onClose={vi.fn()}
        scheduleId="sched-1"
        onCreated={vi.fn()}
      />
    );

    const weeklyBtn = screen.getByTestId("recurrence-option-weekly");
    fireEvent.click(weeklyBtn);

    expect(screen.getByText("Days of the Week")).toBeInTheDocument();
    expect(screen.getByTestId("day-toggle-mon")).toBeInTheDocument();
    expect(screen.getByTestId("day-toggle-fri")).toBeInTheDocument();
  });

  it("submitting form invokes createSlotRule and onCreated", async () => {
    const onCreated = vi.fn();
    const onClose = vi.fn();
    const mockCreatedRule: schedulesApi.ScheduleSlotRuleDto = {
      id: "rule-new-1",
      scheduleId: "sched-1",
      recurrenceType: "Daily",
      cronExpression: "0 9 * * *",
      startTime: "09:00:00",
      endTime: "10:00:00",
      postCapacity: 4,
      createdAt: "2026-09-01T00:00:00Z"
    };

    vi.mocked(schedulesApi.createSlotRule).mockResolvedValueOnce(mockCreatedRule);

    render(
      <SlotRuleFormModal
        isOpen={true}
        onClose={onClose}
        scheduleId="sched-1"
        onCreated={onCreated}
      />
    );

    const submitBtn = screen.getByRole("button", { name: /Create Slot Rule/i });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(schedulesApi.createSlotRule).toHaveBeenCalledWith("sched-1", {
        recurrenceType: "Daily",
        startTime: "09:00:00",
        endTime: "10:00:00",
        postCapacity: 4
      });
      expect(onCreated).toHaveBeenCalledWith(mockCreatedRule);
      expect(onClose).toHaveBeenCalled();
    });
  });
});
