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

  it("paginates drafts when total items exceed page size (12)", () => {
    const manyDrafts = Array.from({ length: 15 }, (_, i) => ({
      id: `draft-${i + 1}`,
      title: `Draft Number ${i + 1}`,
      status: "Draft",
      content: `Content for draft ${i + 1}`,
      targetPlatform: "Bluesky",
      canonicalDraftId: null,
      automationWorkflowId: null,
      threads: [],
      createdAt: `2026-08-${String(i + 1).padStart(2, "0")}T12:00:00Z`,
      updatedAt: `2026-08-${String(i + 1).padStart(2, "0")}T12:00:00Z`,
    }));

    useDraftStore.setState({
      drafts: manyDrafts,
      loadDrafts,
      createDraft,
    });

    render(<DraftsPage />);

    // Page 1 should display Draft Number 1 through 12, but not 13
    expect(screen.getByText("Draft Number 1")).toBeInTheDocument();
    expect(screen.getByText("Draft Number 12")).toBeInTheDocument();
    expect(screen.queryByText("Draft Number 13")).not.toBeInTheDocument();

    expect(screen.getByText("Page 1 of 2 (15 items)")).toBeInTheDocument();
    const prevBtn = screen.getByRole("button", { name: /prev/i });
    const nextBtn = screen.getByRole("button", { name: /next/i });

    expect(prevBtn).toBeDisabled();
    expect(nextBtn).not.toBeDisabled();

    // Click Next
    fireEvent.click(nextBtn);

    // Page 2 should display Draft Number 13 through 15, and not Draft Number 1
    expect(screen.getByText("Draft Number 13")).toBeInTheDocument();
    expect(screen.getByText("Draft Number 15")).toBeInTheDocument();
    expect(screen.queryByText("Draft Number 1")).not.toBeInTheDocument();
    expect(screen.getByText("Page 2 of 2 (15 items)")).toBeInTheDocument();
    expect(nextBtn).toBeDisabled();
    expect(prevBtn).not.toBeDisabled();

    // Click Prev
    fireEvent.click(prevBtn);
    expect(screen.getByText("Draft Number 1")).toBeInTheDocument();
    expect(screen.queryByText("Draft Number 13")).not.toBeInTheDocument();
  });

  it("resets page to 1 when search or tab filter changes", () => {
    const manyDrafts = Array.from({ length: 15 }, (_, i) => ({
      id: `draft-${i + 1}`,
      title: i === 14 ? "Special Searchable Draft" : `Draft Number ${i + 1}`,
      status: "Draft",
      content: `Content ${i + 1}`,
      targetPlatform: "Bluesky",
      canonicalDraftId: null,
      automationWorkflowId: null,
      threads: [],
      createdAt: `2026-08-01T12:00:00Z`,
      updatedAt: `2026-08-01T12:00:00Z`,
    }));

    useDraftStore.setState({
      drafts: manyDrafts,
      loadDrafts,
      createDraft,
    });

    render(<DraftsPage />);

    // Navigate to page 2
    fireEvent.click(screen.getByRole("button", { name: /next/i }));
    expect(screen.getByText("Page 2 of 2 (15 items)")).toBeInTheDocument();

    // Type in search bar
    const searchInput = screen.getByPlaceholderText("Search drafts...");
    fireEvent.change(searchInput, { target: { value: "Special" } });

    // Should reset page and show filtered result
    expect(screen.getByText("Special Searchable Draft")).toBeInTheDocument();
  });
});
