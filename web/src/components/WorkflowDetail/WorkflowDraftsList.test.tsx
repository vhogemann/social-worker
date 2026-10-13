import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { WorkflowDraftsList } from "./WorkflowDraftsList";
import { type WorkflowDraftSummaryDto } from "../../api/workflows";

const mockNavigate = vi.fn();
vi.mock("react-router-dom", () => ({
  useNavigate: () => mockNavigate,
}));

describe("WorkflowDraftsList", () => {
  const onTrigger = vi.fn();
  const onRetryFailed = vi.fn();
  const onRetryDraft = vi.fn();

  const mockDrafts: WorkflowDraftSummaryDto[] = [
    {
      id: "draft-active-1",
      title: "Active Tech Highlights",
      status: "Formatting",
      targetPlatform: null,
      threads: [
        { platform: "Twitter", stage: "Draft", accountHandle: "testuser", variantId: "var-1" }
      ],
      createdAt: "2026-08-20T10:00:00Z",
      updatedAt: "2026-08-20T10:00:00Z",
    },
    {
      id: "draft-discarded-1",
      title: "Discarded Routine Article",
      status: "Discarded",
      targetPlatform: null,
      threads: [],
      discardReason: "Not enough political weight or debate angle for Twitter.",
      relevanceScore: 3,
      createdAt: "2026-08-21T10:00:00Z",
      updatedAt: "2026-08-21T10:00:00Z",
    },
    {
      id: "draft-archived-1",
      title: "Old Archived Campaign",
      status: "Archived",
      targetPlatform: null,
      threads: [],
      createdAt: "2026-08-19T10:00:00Z",
      updatedAt: "2026-08-19T10:00:00Z",
    },
    {
      id: "draft-failed-1",
      title: "Failed News Generation",
      status: "Failed",
      targetPlatform: null,
      threads: [],
      createdAt: "2026-08-22T10:00:00Z",
      updatedAt: "2026-08-22T10:00:00Z",
    }
  ];

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders filter tabs with counts and defaults to All Active tab", () => {
    render(
      <WorkflowDraftsList
        workflowId="wf-1"
        drafts={mockDrafts}
        onTrigger={onTrigger}
        onRetryFailed={onRetryFailed}
        onRetryDraft={onRetryDraft}
      />
    );

    // Filter tab buttons with counts
    expect(screen.getByTestId("filter-tab-all")).toHaveTextContent("All Active (2)");
    expect(screen.getByTestId("filter-tab-discarded")).toHaveTextContent("Discarded (1)");
    expect(screen.getByTestId("filter-tab-archived")).toHaveTextContent("Archived (1)");

    // In All Active (default): Active and Failed should be visible, Discarded and Archived hidden
    expect(screen.getByText("Active Tech Highlights")).toBeInTheDocument();
    expect(screen.getByText("Failed News Generation")).toBeInTheDocument();
    expect(screen.queryByText("Discarded Routine Article")).not.toBeInTheDocument();
    expect(screen.queryByText("Old Archived Campaign")).not.toBeInTheDocument();
  });

  it("switches to Discarded tab and displays discard reason and score", () => {
    render(
      <WorkflowDraftsList
        workflowId="wf-1"
        drafts={mockDrafts}
        onTrigger={onTrigger}
      />
    );

    fireEvent.click(screen.getByTestId("filter-tab-discarded"));

    expect(screen.getByText("Discarded Routine Article")).toBeInTheDocument();
    expect(screen.getByText(/Not enough political weight/i)).toBeInTheDocument();
    expect(screen.getByText("Discarded (3/10)")).toBeInTheDocument();

    expect(screen.queryByText("Active Tech Highlights")).not.toBeInTheDocument();
    expect(screen.queryByText("Failed News Generation")).not.toBeInTheDocument();
  });

  it("switches to Archived tab and displays archived drafts", () => {
    render(
      <WorkflowDraftsList
        workflowId="wf-1"
        drafts={mockDrafts}
        onTrigger={onTrigger}
      />
    );

    fireEvent.click(screen.getByTestId("filter-tab-archived"));

    expect(screen.getByText("Old Archived Campaign")).toBeInTheDocument();
    expect(screen.queryByText("Active Tech Highlights")).not.toBeInTheDocument();
    expect(screen.queryByText("Discarded Routine Article")).not.toBeInTheDocument();
  });

  it("filters drafts using the search bar", () => {
    render(
      <WorkflowDraftsList
        workflowId="wf-1"
        drafts={mockDrafts}
        onTrigger={onTrigger}
      />
    );

    const searchInput = screen.getByTestId("workflow-drafts-search-input");
    fireEvent.change(searchInput, { target: { value: "highlights" } });

    expect(screen.getByText("Active Tech Highlights")).toBeInTheDocument();
    expect(screen.queryByText("Failed News Generation")).not.toBeInTheDocument();
  });

  it("renders empty state when tab has no drafts and provides clear action", () => {
    const onlyActiveDrafts = [mockDrafts[0]];

    render(
      <WorkflowDraftsList
        workflowId="wf-1"
        drafts={onlyActiveDrafts}
        onTrigger={onTrigger}
      />
    );

    fireEvent.click(screen.getByTestId("filter-tab-discarded"));

    expect(screen.getByText("No discarded drafts")).toBeInTheDocument();
    expect(screen.getByText("View All Active")).toBeInTheDocument();

    fireEvent.click(screen.getByText("View All Active"));
    expect(screen.getByText("Active Tech Highlights")).toBeInTheDocument();
  });

  it("navigates to workflow draft on card click", () => {
    render(
      <WorkflowDraftsList
        workflowId="wf-1"
        drafts={mockDrafts}
        onTrigger={onTrigger}
      />
    );

    fireEvent.click(screen.getByText("Active Tech Highlights"));
    expect(mockNavigate).toHaveBeenCalledWith("/workflows/wf-1/draft/draft-active-1");
  });

  it("navigates to variant on thread badge click", () => {
    render(
      <WorkflowDraftsList
        workflowId="wf-1"
        drafts={mockDrafts}
        onTrigger={onTrigger}
      />
    );

    const threadBadge = screen.getByTestId("workflow-draft-thread-twitter");
    fireEvent.click(threadBadge);
    expect(mockNavigate).toHaveBeenCalledWith("/workflows/wf-1/draft/draft-active-1/variant/var-1");
  });
});
