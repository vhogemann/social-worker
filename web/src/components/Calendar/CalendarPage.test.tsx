import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { CalendarPage } from "./CalendarPage";
import * as schedulesApi from "../../api/schedules";

vi.mock("../../api/schedules");

const mockSchedules: schedulesApi.ScheduleSummaryDto[] = [
  {
    id: "sched-1",
    name: "Tech Beat",
    timezone: "Europe/London",
    isActive: true,
    slotRuleCount: 2,
    queueItemCount: 1,
    createdAt: "2026-09-01T00:00:00Z",
    updatedAt: "2026-09-01T00:00:00Z"
  },
  {
    id: "sched-2",
    name: "Weekend Digest",
    timezone: "America/New_York",
    isActive: true,
    slotRuleCount: 1,
    queueItemCount: 0,
    createdAt: "2026-09-01T00:00:00Z",
    updatedAt: "2026-09-01T00:00:00Z"
  }
];

const mockEvents: schedulesApi.CalendarEventDto[] = [
  {
    id: "evt-1",
    type: "ScheduledPost",
    timestampUtc: "2026-09-07T09:00:00Z",
    title: "Monday Kickoff Thread",
    platform: "Bluesky",
    scheduleId: "sched-1",
    scheduleName: "Tech Beat"
  }
];

describe("CalendarPage", () => {
  const fixedDate = new Date(2026, 8, 7); // Monday Sep 7, 2026

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(schedulesApi.fetchScheduleQueue).mockResolvedValue([]);
    vi.mocked(schedulesApi.fetchSchedule).mockResolvedValue({
      id: "sched-1",
      name: "Tech Beat",
      timezone: "Europe/London",
      isActive: true,
      slotRules: [],
      queueItems: [],
      createdAt: "2026-09-01T00:00:00Z",
      updatedAt: "2026-09-01T00:00:00Z"
    });
  });

  it("loads schedules, sets first schedule as default, and displays timezone badge", async () => {
    vi.mocked(schedulesApi.fetchSchedules).mockResolvedValueOnce(mockSchedules);
    vi.mocked(schedulesApi.fetchCalendarFeed).mockResolvedValue(mockEvents);

    render(
      <MemoryRouter>
        <CalendarPage initialDate={fixedDate} initialView="week" />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText("Content Calendar")).toBeInTheDocument();
      expect(screen.getByTestId("schedule-selector")).toHaveTextContent("Tech Beat");
      expect(screen.getByText("Europe/London")).toBeInTheDocument();
    });
  });

  it("switching view toggles between week, day, and month views", async () => {
    vi.mocked(schedulesApi.fetchSchedules).mockResolvedValueOnce(mockSchedules);
    vi.mocked(schedulesApi.fetchCalendarFeed).mockResolvedValue(mockEvents);

    render(
      <MemoryRouter>
        <CalendarPage initialDate={fixedDate} initialView="week" />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByTestId("view-toggle-week")).toBeInTheDocument();
    });

    // Switch to month view
    const monthToggle = screen.getByTestId("view-toggle-month");
    fireEvent.click(monthToggle);

    await waitFor(() => {
      expect(screen.getByText("Mon")).toBeInTheDocument();
      expect(screen.getByText("Sun")).toBeInTheDocument();
    });

    // Switch to day view
    const dayToggle = screen.getByTestId("view-toggle-day");
    fireEvent.click(dayToggle);

    await waitFor(() => {
      expect(screen.getByTestId("cal-period-label")).toHaveTextContent("Monday, September 7, 2026");
      expect(screen.getByRole("heading", { name: /Monday, September 7, 2026/i })).toBeInTheDocument();
    });
  });

  it("navigating prev and next updates period label", async () => {
    vi.mocked(schedulesApi.fetchSchedules).mockResolvedValueOnce(mockSchedules);
    vi.mocked(schedulesApi.fetchCalendarFeed).mockResolvedValue(mockEvents);

    render(
      <MemoryRouter>
        <CalendarPage initialDate={fixedDate} initialView="day" />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByTestId("cal-period-label")).toHaveTextContent("September 7, 2026");
    });

    // Next day
    const nextBtn = screen.getByTestId("cal-nav-next");
    fireEvent.click(nextBtn);

    await waitFor(() => {
      expect(screen.getByTestId("cal-period-label")).toHaveTextContent("September 8, 2026");
    });

    // Prev day
    const prevBtn = screen.getByTestId("cal-nav-prev");
    fireEvent.click(prevBtn);

    await waitFor(() => {
      expect(screen.getByTestId("cal-period-label")).toHaveTextContent("September 7, 2026");
    });
  });

  it("displays error banner if fetching feed fails", async () => {
    vi.mocked(schedulesApi.fetchSchedules).mockResolvedValueOnce(mockSchedules);
    vi.mocked(schedulesApi.fetchCalendarFeed).mockRejectedValueOnce(new Error("Network disconnect"));

    render(
      <MemoryRouter>
        <CalendarPage initialDate={fixedDate} />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText("Network disconnect")).toBeInTheDocument();
    });
  });

  it("clicking toolbar Add Slot button opens SlotRuleFormModal", async () => {
    vi.mocked(schedulesApi.fetchSchedules).mockResolvedValueOnce(mockSchedules);
    vi.mocked(schedulesApi.fetchCalendarFeed).mockResolvedValue(mockEvents);

    render(
      <MemoryRouter>
        <CalendarPage initialDate={fixedDate} initialView="week" />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByTestId("toolbar-add-slot-btn")).toBeInTheDocument();
      expect(screen.getByTestId("open-schedule-manager-btn")).toHaveTextContent("Settings");
      expect(screen.getByTestId("schedule-selector")).toHaveValue("sched-1");
    });

    fireEvent.click(screen.getByTestId("toolbar-add-slot-btn"));

    await waitFor(() => {
      expect(screen.getByTestId("slot-rule-modal")).toBeInTheDocument();
      expect(screen.getByText("Add Publishing Slot Rule")).toBeInTheDocument();
    });
  });

  it("clicking cell Add Slot button in week view opens SlotRuleFormModal prefilled", async () => {
    vi.mocked(schedulesApi.fetchSchedules).mockResolvedValueOnce(mockSchedules);
    vi.mocked(schedulesApi.fetchCalendarFeed).mockResolvedValue(mockEvents);

    render(
      <MemoryRouter>
        <CalendarPage initialDate={fixedDate} initialView="week" />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByTestId("schedule-selector")).toHaveValue("sched-1");
    });

    const cellBtn = await screen.findByTestId("add-slot-btn-2026-09-07-14");
    fireEvent.click(cellBtn);

    await waitFor(() => {
      expect(screen.getByTestId("slot-rule-modal")).toBeInTheDocument();
      const startInput = screen.getByTestId("start-time-input") as HTMLInputElement;
      const endInput = screen.getByTestId("end-time-input") as HTMLInputElement;
      expect(startInput.value).toBe("14:00");
      expect(endInput.value).toBe("15:00");
    });
  });
});
