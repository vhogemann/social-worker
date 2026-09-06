import React from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { WeekView } from "./WeekView";
import type { CalendarEventDto } from "../../api/schedules";

describe("WeekView", () => {
  const mondayDate = new Date(2026, 8, 7); // Mon Sep 7, 2026

  const mockEvents: CalendarEventDto[] = [
    {
      id: "evt-sched-1",
      type: "ScheduledPost",
      timestampUtc: "2026-09-07T09:00:00Z",
      title: "Weekly Launch Post",
      platform: "Bluesky",
      subSlotIndex: 0,
      totalCapacity: 4
    },
    {
      id: "evt-open-1",
      type: "OpenSlot",
      timestampUtc: "2026-09-07T09:15:00Z",
      subSlotIndex: 1,
      totalCapacity: 4
    },
    {
      id: "evt-pub-1",
      type: "PublishedPost",
      timestampUtc: "2026-09-08T10:00:00Z",
      title: "Tuesday Live Update",
      platform: "Bluesky"
    }
  ];

  it("renders week day headers for Mon-Sun", () => {
    render(
      <WeekView
        currentDate={mondayDate}
        events={[]}
        onSelectDate={vi.fn()}
      />
    );

    expect(screen.getByTestId("week-header-2026-09-07")).toBeInTheDocument();
    expect(screen.getByTestId("week-header-2026-09-13")).toBeInTheDocument();
  });

  it("renders scheduled post, open slot, and published post", () => {
    render(
      <WeekView
        currentDate={mondayDate}
        events={mockEvents}
        onSelectDate={vi.fn()}
      />
    );

    expect(screen.getByText("Weekly Launch Post")).toBeInTheDocument();
    expect(screen.getByText("1/4")).toBeInTheDocument();
    expect(screen.getByText("+ Queue Draft")).toBeInTheDocument();
    expect(screen.getByText("Tuesday Live Update")).toBeInTheDocument();
  });

  it("clicking day header calls onSelectDate", () => {
    const onSelect = vi.fn();
    render(
      <WeekView
        currentDate={mondayDate}
        events={[]}
        onSelectDate={onSelect}
      />
    );

    const header = screen.getByTestId("week-header-2026-09-07");
    fireEvent.click(header);

    expect(onSelect).toHaveBeenCalledTimes(1);
    expect(onSelect.mock.calls[0][0].getDate()).toBe(7);
  });

  it("clicking scheduled post triggers onEventClick", () => {
    const onEventClick = vi.fn();
    render(
      <WeekView
        currentDate={mondayDate}
        events={mockEvents}
        onSelectDate={vi.fn()}
        onEventClick={onEventClick}
      />
    );

    const post = screen.getByTestId("week-event-scheduled-evt-sched-1");
    fireEvent.click(post);

    expect(onEventClick).toHaveBeenCalledWith(mockEvents[0]);
  });

  it("clicking open slot triggers onOpenSlotClick", () => {
    const onOpenSlotClick = vi.fn();
    render(
      <WeekView
        currentDate={mondayDate}
        events={mockEvents}
        onSelectDate={vi.fn()}
        onOpenSlotClick={onOpenSlotClick}
      />
    );

    const openSlot = screen.getByTestId("week-event-open-evt-open-1");
    fireEvent.click(openSlot);

    expect(onOpenSlotClick).toHaveBeenCalledWith(mockEvents[1]);
  });
});
