import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { DraftsPage } from "./DraftsPage";
import { useDraftStore } from "../../store/draftStore";

const mockNavigate = vi.fn();
vi.mock("react-router-dom", () => ({
  useNavigate: () => mockNavigate,
}));

describe("DraftsPage", () => {
  const loadDrafts = vi.fn();
  const createDraft = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    useDraftStore.setState({
      drafts: [
        {
          id: "draft-1",
          title: "Standalone Draft",
          status: "Draft",
          content: "Content 1",
          targetPlatform: "Bluesky",
          canonicalDraftId: null,
          automationWorkflowId: null,
          threads: [],
          createdAt: "2026-08-01T12:00:00Z",
          updatedAt: "2026-08-01T12:00:00Z"
        },
        {
          id: "draft-2",
          title: "Workflow Draft",
          status: "Draft",
          content: "Content 2",
          targetPlatform: "Twitter",
          canonicalDraftId: null,
          automationWorkflowId: "wf-1",
          automationWorkflowTitle: "Daily News",
          threads: [],
          createdAt: "2026-08-02T12:00:00Z",
          updatedAt: "2026-08-02T12:00:00Z"
        },
      ],
      loadDrafts,
      createDraft,
    });
  });

  it("renders drafts library header and filters by tabs", () => {
    render(<DraftsPage />);

    expect(screen.getByText("Drafts Library")).toBeInTheDocument();
    expect(screen.getByText("Standalone Draft")).toBeInTheDocument();
    expect(screen.getByText("Workflow Draft")).toBeInTheDocument();

    // Click Standalone tab
    fireEvent.click(screen.getByRole("button", { name: "Standalone" }));
    expect(screen.getByText("Standalone Draft")).toBeInTheDocument();
    expect(screen.queryByText("Workflow Draft")).not.toBeInTheDocument();

    // Click Workflow Generated tab
    fireEvent.click(screen.getByRole("button", { name: "Workflow Generated" }));
    expect(screen.queryByText("Standalone Draft")).not.toBeInTheDocument();
    expect(screen.getByText("Workflow Draft")).toBeInTheDocument();
  });

  it("navigates to workflow draft route for workflow draft", () => {
    render(<DraftsPage />);

    fireEvent.click(screen.getByText("Workflow Draft"));
    expect(mockNavigate).toHaveBeenCalledWith("/workflows/wf-1/draft/draft-2");
  });

  it("navigates to standalone draft route for standalone draft", () => {
    render(<DraftsPage />);

    fireEvent.click(screen.getByText("Standalone Draft"));
    expect(mockNavigate).toHaveBeenCalledWith("/draft/draft-1");
  });
});
