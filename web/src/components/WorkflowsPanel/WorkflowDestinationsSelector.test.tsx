import React from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import {
  WorkflowDestinationsSelector,
  type WorkflowDestinationItem
} from "./WorkflowDestinationsSelector";
import { type AccountDto } from "../../api/accounts";
import { type ScheduleSummaryDto } from "../../api/schedules";

const mockAccounts: AccountDto[] = [
  {
    id: "acc-1",
    platform: "Bluesky",
    handle: "alice.bsky.social",
    status: "Active",
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: "2026-01-01T00:00:00Z"
  },
  {
    id: "acc-2",
    platform: "Twitter",
    handle: "alice_tweets",
    status: "Active",
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: "2026-01-01T00:00:00Z"
  }
];

const mockSchedules: ScheduleSummaryDto[] = [
  {
    id: "sched-1",
    name: "Morning Digest",
    timezone: "America/New_York",
    isActive: true,
    slotRuleCount: 2,
    queueItemCount: 1,
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: "2026-01-01T00:00:00Z"
  },
  {
    id: "sched-2",
    name: "Evening Round-up",
    timezone: "UTC",
    isActive: true,
    slotRuleCount: 1,
    queueItemCount: 0,
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: "2026-01-01T00:00:00Z"
  }
];

describe("WorkflowDestinationsSelector", () => {
  it("renders destination cards for connected accounts", () => {
    render(
      <WorkflowDestinationsSelector
        connectedAccounts={mockAccounts}
        destinations={[]}
        onToggleDestination={vi.fn()}
      />
    );

    expect(screen.getByText("Bluesky")).toBeInTheDocument();
    expect(screen.getByText("@alice.bsky.social")).toBeInTheDocument();
    expect(screen.getByText("Twitter")).toBeInTheDocument();
    expect(screen.getByText("@alice_tweets")).toBeInTheDocument();
  });

  it("toggles destination when Switch is clicked", () => {
    const handleToggle = vi.fn();

    render(
      <WorkflowDestinationsSelector
        connectedAccounts={mockAccounts}
        destinations={[]}
        onToggleDestination={handleToggle}
      />
    );

    const bskySwitch = screen.getByTestId("toggle-dest-bluesky");
    expect(bskySwitch).not.toBeChecked();

    fireEvent.click(bskySwitch);
    expect(handleToggle).toHaveBeenCalledWith("Bluesky", "acc-1", "@alice.bsky.social");
  });

  it("displays publishing modes and description when enabled", () => {
    const destinations: WorkflowDestinationItem[] = [
      {
        platform: "Bluesky",
        autoPublish: false,
        publishingMode: "Manual",
        accountId: "acc-1",
        accountHandle: "alice.bsky.social"
      }
    ];

    render(
      <WorkflowDestinationsSelector
        connectedAccounts={mockAccounts}
        destinations={destinations}
        onToggleDestination={vi.fn()}
      />
    );

    expect(screen.getByTestId("toggle-dest-bluesky")).toBeChecked();
    expect(screen.getByTestId("mode-manual-bluesky")).toBeInTheDocument();
    expect(screen.getByTestId("mode-automatic-bluesky")).toBeInTheDocument();
    expect(screen.getByTestId("mode-scheduled-bluesky")).toBeInTheDocument();
    expect(screen.getByText(/manual review:/i)).toBeInTheDocument();
  });

  it("changes publishing mode when button is clicked", () => {
    const handleChangeMode = vi.fn();
    const destinations: WorkflowDestinationItem[] = [
      {
        platform: "Bluesky",
        autoPublish: false,
        publishingMode: "Manual",
        accountId: "acc-1",
        accountHandle: "alice.bsky.social"
      }
    ];

    render(
      <WorkflowDestinationsSelector
        connectedAccounts={mockAccounts}
        destinations={destinations}
        availableSchedules={mockSchedules}
        onToggleDestination={vi.fn()}
        onChangePublishingMode={handleChangeMode}
      />
    );

    fireEvent.click(screen.getByTestId("mode-automatic-bluesky"));
    expect(handleChangeMode).toHaveBeenCalledWith("Bluesky", "Automatic", undefined, "acc-1");

    fireEvent.click(screen.getByTestId("mode-scheduled-bluesky"));
    expect(handleChangeMode).toHaveBeenCalledWith("Bluesky", "Scheduled", "sched-1", "acc-1");
  });

  it("renders schedule selector when mode is Scheduled", () => {
    const destinations: WorkflowDestinationItem[] = [
      {
        platform: "Bluesky",
        autoPublish: false,
        publishingMode: "Scheduled",
        scheduleId: "sched-1",
        accountId: "acc-1",
        accountHandle: "alice.bsky.social"
      }
    ];

    render(
      <WorkflowDestinationsSelector
        connectedAccounts={mockAccounts}
        destinations={destinations}
        availableSchedules={mockSchedules}
        onToggleDestination={vi.fn()}
      />
    );

    expect(screen.getByTestId("schedule-select-bluesky")).toBeInTheDocument();
    expect(screen.getByText(/add to schedule:/i)).toBeInTheDocument();
  });

  it("shows warning when Scheduled mode has no available schedules", () => {
    const destinations: WorkflowDestinationItem[] = [
      {
        platform: "Bluesky",
        autoPublish: false,
        publishingMode: "Scheduled",
        accountId: "acc-1",
        accountHandle: "alice.bsky.social"
      }
    ];

    render(
      <WorkflowDestinationsSelector
        connectedAccounts={mockAccounts}
        destinations={destinations}
        availableSchedules={[]}
        onToggleDestination={vi.fn()}
      />
    );

    expect(
      screen.getByText(/no active schedules found\. please create a schedule in the calendar first\./i)
    ).toBeInTheDocument();
  });

  it("renders default Bluesky row when no accounts are connected", () => {
    render(
      <WorkflowDestinationsSelector
        connectedAccounts={[]}
        destinations={[]}
        onToggleDestination={vi.fn()}
      />
    );

    expect(screen.getByText("Bluesky")).toBeInTheDocument();
    expect(screen.getByText("(Default)")).toBeInTheDocument();
  });
});
