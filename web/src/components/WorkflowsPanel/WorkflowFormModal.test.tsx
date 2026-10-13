import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { WorkflowFormModal } from "./WorkflowFormModal";
import { type FeedSubscriptionDto } from "../../api/feeds";
import { type AccountDto } from "../../api/accounts";
import { type AutomationWorkflowDto } from "../../api/workflows";

import * as schedulesApi from "../../api/schedules";

vi.mock("../../api/schedules");

const mockFeeds: FeedSubscriptionDto[] = [
  {
    id: "feed-1",
    title: "AI Daily",
    feedUrl: "https://example.com/ai.xml",
    websiteUrl: "https://example.com",
    lastPolledAt: null,
    createdAt: "2026-08-01T12:00:00Z"
  }
];

const mockAccounts: AccountDto[] = [
  {
    id: "acc-1",
    platform: "Bluesky",
    handle: "alice.bsky.social",
    status: "Active",
    createdAt: "2026-08-01T12:00:00Z",
    updatedAt: "2026-08-01T12:00:00Z"
  }
];

const mockSchedules: schedulesApi.ScheduleSummaryDto[] = [
  {
    id: "sched-1",
    name: "Tech Digest",
    timezone: "UTC",
    isActive: true,
    slotRuleCount: 1,
    queueItemCount: 0,
    createdAt: "2026-09-01T00:00:00Z",
    updatedAt: "2026-09-01T00:00:00Z"
  }
];

const mockWorkflows: AutomationWorkflowDto[] = [
  {
    id: "wf-1",
    title: "Source Feed Workflow",
    instructionPrompt: "Summarize article",
    sources: [{ feedSubscriptionId: "feed-1", title: "AI Daily", feedUrl: "https://example.com/ai.xml" }],
    destinations: [{ id: "dest-1", platform: "Bluesky", autoPublish: false }],
    createdAt: "2026-09-01T00:00:00Z",
    updatedAt: "2026-09-01T00:00:00Z"
  }
];

describe("WorkflowFormModal", () => {
  beforeEach(() => {
    vi.mocked(schedulesApi.fetchSchedules).mockResolvedValue(mockSchedules);
  });

  it("renders and submits workflow creation data with default Manual mode", async () => {
    const handleSave = vi.fn().mockResolvedValue(undefined);
    const handleClose = vi.fn();

    render(
      <WorkflowFormModal
        isOpen={true}
        editingWorkflow={null}
        availableFeeds={mockFeeds}
        availableWorkflows={mockWorkflows}
        connectedAccounts={mockAccounts}
        onClose={handleClose}
        onSave={handleSave}
      />
    );

    expect(screen.getByText("Create Automation Workflow")).toBeInTheDocument();
    const titleInput = screen.getByLabelText(/workflow title/i);
    fireEvent.change(titleInput, { target: { value: "Tech News Auto-Draft" } });

    const submitBtn = screen.getByRole("button", { name: /create workflow/i });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(handleSave).toHaveBeenCalledWith({
        title: "Tech News Auto-Draft",
        instructionPrompt: "Summarize this article as an engaging thread.",
        sources: [
          {
            kind: "Feed",
            feedSubscriptionId: "feed-1"
          }
        ],
        destinations: [
          {
            platform: "Bluesky",
            autoPublish: false,
            publishingMode: "Manual",
            accountId: "acc-1",
            accountHandle: "alice.bsky.social"
          }
        ],
        scheduleId: null,
        minRelevanceScore: 7
      });
      expect(handleClose).toHaveBeenCalled();
    });
  });

  it("switches destination publishing mode to Scheduled with target schedule", async () => {
    const handleSave = vi.fn().mockResolvedValue(undefined);
    const handleClose = vi.fn();

    render(
      <WorkflowFormModal
        isOpen={true}
        editingWorkflow={null}
        availableFeeds={mockFeeds}
        availableWorkflows={mockWorkflows}
        connectedAccounts={mockAccounts}
        onClose={handleClose}
        onSave={handleSave}
      />
    );

    const titleInput = screen.getByLabelText(/workflow title/i);
    fireEvent.change(titleInput, { target: { value: "Scheduled Feed Workflow" } });

    await waitFor(() => {
      expect(screen.getByTestId("mode-scheduled-bluesky")).toBeInTheDocument();
    });

    // Select "Add to Schedule" mode
    fireEvent.click(screen.getByTestId("mode-scheduled-bluesky"));

    const submitBtn = screen.getByRole("button", { name: /create workflow/i });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(handleSave).toHaveBeenCalledWith({
        title: "Scheduled Feed Workflow",
        instructionPrompt: "Summarize this article as an engaging thread.",
        sources: [
          {
            kind: "Feed",
            feedSubscriptionId: "feed-1"
          }
        ],
        destinations: [
          {
            platform: "Bluesky",
            autoPublish: false,
            publishingMode: "Scheduled",
            scheduleId: "sched-1",
            accountId: "acc-1",
            accountHandle: "alice.bsky.social"
          }
        ],
        scheduleId: null,
        minRelevanceScore: 7
      });
    });
  });

  it("switches to Workflow Highlights mode and submits aggregated source workflow", async () => {
    const handleSave = vi.fn().mockResolvedValue(undefined);
    const handleClose = vi.fn();

    render(
      <WorkflowFormModal
        isOpen={true}
        editingWorkflow={null}
        availableFeeds={mockFeeds}
        availableWorkflows={mockWorkflows}
        connectedAccounts={mockAccounts}
        onClose={handleClose}
        onSave={handleSave}
      />
    );

    const titleInput = screen.getByLabelText(/workflow title/i);
    fireEvent.change(titleInput, { target: { value: "Weekly Digest Workflow" } });

    // Switch to Workflow Highlights
    const highlightsTab = screen.getByRole("button", { name: /workflow highlights/i });
    fireEvent.click(highlightsTab);

    // Toggle the source workflow
    fireEvent.click(screen.getByText("Source Feed Workflow"));

    const submitBtn = screen.getByRole("button", { name: /create workflow/i });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(handleSave).toHaveBeenCalledWith({
        title: "Weekly Digest Workflow",
        instructionPrompt: "Summarize this article as an engaging thread.",
        sources: [
          {
            kind: "WorkflowHighlights",
            sourceWorkflowId: "wf-1",
            configurationJson: JSON.stringify({ lookbackDays: 7, sourceWorkflowIds: ["wf-1"] })
          }
        ],
        destinations: [
          {
            platform: "Bluesky",
            autoPublish: false,
            publishingMode: "Manual",
            accountId: "acc-1",
            accountHandle: "alice.bsky.social"
          }
        ],
        scheduleId: null,
        minRelevanceScore: 7
      });
    });
  });

  it("initializes and allows adjusting the relevance threshold slider", async () => {
    const handleSave = vi.fn().mockResolvedValue(undefined);
    const handleClose = vi.fn();

    const workflowWithCustomScore: AutomationWorkflowDto = {
      id: "wf-custom",
      title: "Guerrilha News",
      instructionPrompt: "Virality focus",
      sources: [{ feedSubscriptionId: "feed-1", title: "AI Daily" }],
      destinations: [{ id: "d-1", platform: "Bluesky", autoPublish: false }],
      minRelevanceScore: 9,
      createdAt: "2026-09-01T00:00:00Z",
      updatedAt: "2026-09-01T00:00:00Z"
    };

    render(
      <WorkflowFormModal
        isOpen={true}
        editingWorkflow={workflowWithCustomScore}
        availableFeeds={mockFeeds}
        availableWorkflows={mockWorkflows}
        connectedAccounts={mockAccounts}
        onClose={handleClose}
        onSave={handleSave}
      />
    );

    // Verify initial score badge shows 9/10
    expect(screen.getByTestId("relevance-score-badge")).toHaveTextContent("9/10");

    // Change slider to 0 (Disabled)
    const slider = screen.getByTestId("relevance-score-slider");
    fireEvent.change(slider, { target: { value: "0" } });

    expect(screen.getByTestId("relevance-score-badge")).toHaveTextContent("Off (0/10)");
    expect(screen.getByText(/Filter disabled: All ingested feed items will be processed/i)).toBeInTheDocument();

    const submitBtn = screen.getByRole("button", { name: /save changes/i });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(handleSave).toHaveBeenCalledWith(
        expect.objectContaining({
          title: "Guerrilha News",
          minRelevanceScore: 0
        })
      );
    });
  });
});
