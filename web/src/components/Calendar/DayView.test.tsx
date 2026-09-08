import React from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { DayView } from "./DayView";
import type { CalendarEventDto } from "../../api/schedules";

const mockNavigate = vi.fn();
vi.mock("react-router-dom", async () => {
  const actual = await vi.importActual("react-router-dom");
  return {
    ...actual,
    useNavigate: () => mockNavigate
  };
});

describe("DayView", () => {
  const selectedDate = new Date(2026, 8, 7); // Sep 7, 2026

  const mockEvents: CalendarEventDto[] = [
    {
      id: "evt-1",
      type: "ScheduledPost",
      timestampUtc: "2026-09-07T09:00:00Z",
      title: "Scheduled Tech Thread",
      platform: "Bluesky",
      draftId: "draft-xyz",
      subSlotIndex: 0,
      totalCapacity: 4,
      scheduleName: "Morning Beat"
    },
    {
      id: "evt-2",
      type: "OpenSlot",
      timestampUtc: "2026-09-07T09:15:00Z",
      subSlotIndex: 1,
      totalCapacity: 4,
      scheduleName: "Morning Beat"
    },
    {
      id: "evt-3",
      type: "PublishedPost",
      timestampUtc: "2026-09-07T12:00:00Z",
      title: "Noon Update",
      platform: "Bluesky",
      remoteUrl: "https://bsky.app/profile/test/post/123"
    }
  ];

  it("renders empty state when day has no events", () => {
    render(
      <MemoryRouter>
        <DayView
          currentDate={new Date(2026, 8, 20)}
          events={[]}
        />
      </MemoryRouter>
    );

    expect(screen.getByText("No slots or posts scheduled")).toBeInTheDocument();
  });

  it("renders capacity stats and fill ratio", () => {
    render(
      <MemoryRouter>
        <DayView
          currentDate={selectedDate}
          events={mockEvents}
        />
      </MemoryRouter>
    );

    expect(screen.getByText(/3 total slots/)).toBeInTheDocument();
    expect(screen.getByText(/2 \/ 3 filled/)).toBeInTheDocument();
    expect(screen.getByText(/67% capacity/)).toBeInTheDocument();
  });

  it("renders scheduled post and clicking Edit Draft navigates to composer", () => {
    render(
      <MemoryRouter>
        <DayView
          currentDate={selectedDate}
          events={mockEvents}
        />
      </MemoryRouter>
    );

    expect(screen.getByText("Scheduled Tech Thread")).toBeInTheDocument();
    expect(screen.getByText("Slot 1 of 4")).toBeInTheDocument();
    expect(screen.getByText("Morning Beat")).toBeInTheDocument();

    const editBtn = screen.getByRole("button", { name: /Edit Draft/i });
    fireEvent.click(editBtn);

    expect(mockNavigate).toHaveBeenCalledWith("/draft/draft-xyz");
  });

  it("renders open slot and clicking Queue Draft calls onOpenSlotClick", () => {
    const onOpenSlotClick = vi.fn();
    render(
      <MemoryRouter>
        <DayView
          currentDate={selectedDate}
          events={mockEvents}
          onOpenSlotClick={onOpenSlotClick}
        />
      </MemoryRouter>
    );

    expect(screen.getByText("Slot 2 of 4 available")).toBeInTheDocument();

    const queueBtn = screen.getByRole("button", { name: /Queue Draft/i });
    fireEvent.click(queueBtn);

    expect(onOpenSlotClick).toHaveBeenCalledWith(mockEvents[1]);
  });

  it("renders published post with live link", () => {
    render(
      <MemoryRouter>
        <DayView
          currentDate={selectedDate}
          events={mockEvents}
        />
      </MemoryRouter>
    );

    expect(screen.getByText("Noon Update")).toBeInTheDocument();
    const liveLink = screen.getByRole("link", { name: /View Live/i });
    expect(liveLink).toHaveAttribute("href", "https://bsky.app/profile/test/post/123");
  });

  it("clicking add slot button invokes onAddSlot with current date", () => {
    const onAddSlot = vi.fn();
    render(
      <MemoryRouter>
        <DayView
          currentDate={selectedDate}
          events={mockEvents}
          onAddSlot={onAddSlot}
        />
      </MemoryRouter>
    );

    const addBtn = screen.getByTestId("day-add-slot-btn");
    fireEvent.click(addBtn);

    expect(onAddSlot).toHaveBeenCalledWith(selectedDate);
  });
});
