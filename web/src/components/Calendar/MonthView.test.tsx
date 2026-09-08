import React from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { MonthView } from "./MonthView";
import type { CalendarEventDto } from "../../api/schedules";

describe("MonthView", () => {
  const baseDate = new Date(2026, 8, 7); // Sep 7, 2026

  const mockEvents: CalendarEventDto[] = [
    {
      id: "event-1",
      type: "ScheduledPost",
      timestampUtc: "2026-09-07T09:00:00Z",
      title: "First Scheduled Thread",
      platform: "Bluesky"
    },
    {
      id: "event-2",
      type: "OpenSlot",
      timestampUtc: "2026-09-07T09:15:00Z"
    },
    {
      id: "event-3",
      type: "PublishedPost",
      timestampUtc: "2026-09-07T09:30:00Z",
      title: "Live Published Post",
      platform: "Bluesky"
    },
    {
      id: "event-4",
      type: "OpenSlot",
      timestampUtc: "2026-09-07T09:45:00Z"
    }
  ];

  it("renders weekday header columns", () => {
    render(
      <MonthView
        currentDate={baseDate}
        events={[]}
        onSelectDate={vi.fn()}
      />
    );

    expect(screen.getByText("Mon")).toBeInTheDocument();
    expect(screen.getByText("Tue")).toBeInTheDocument();
    expect(screen.getByText("Wed")).toBeInTheDocument();
    expect(screen.getByText("Thu")).toBeInTheDocument();
    expect(screen.getByText("Fri")).toBeInTheDocument();
    expect(screen.getByText("Sat")).toBeInTheDocument();
    expect(screen.getByText("Sun")).toBeInTheDocument();
  });

  it("renders events inside the correct day cell", () => {
    render(
      <MonthView
        currentDate={baseDate}
        events={mockEvents}
        onSelectDate={vi.fn()}
      />
    );

    expect(screen.getByText("First Scheduled Thread")).toBeInTheDocument();
    expect(screen.getByText("Live Published Post")).toBeInTheDocument();
    // 4th event is hidden under +1 more
    expect(screen.getByText("+1 more")).toBeInTheDocument();
  });

  it("clicking day cell triggers onSelectDate", () => {
    const onSelect = vi.fn();
    render(
      <MonthView
        currentDate={baseDate}
        events={[]}
        onSelectDate={onSelect}
      />
    );

    const cell = screen.getByTestId("month-cell-2026-09-07");
    fireEvent.click(cell);

    expect(onSelect).toHaveBeenCalledTimes(1);
    expect(onSelect.mock.calls[0][0].getDate()).toBe(7);
  });

  it("clicking add slot button on a day cell calls onAddSlot with cell date", () => {
    const onAddSlot = vi.fn();
    render(
      <MonthView
        currentDate={baseDate}
        events={[]}
        onSelectDate={vi.fn()}
        onAddSlot={onAddSlot}
      />
    );

    const addBtn = screen.getByTestId("add-slot-month-btn-2026-09-07");
    fireEvent.click(addBtn);

    expect(onAddSlot).toHaveBeenCalledTimes(1);
    expect(onAddSlot.mock.calls[0][0].getDate()).toBe(7);
  });
});
