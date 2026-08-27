import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import React from "react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { WorkflowDetailPage } from "./WorkflowDetailPage";
import * as workflowsApi from "../../api/workflows";
import * as feedsApi from "../../api/feeds";
import * as accountsApi from "../../api/accounts";

vi.mock("../../api/workflows", () => ({
  fetchWorkflow: vi.fn(),
  fetchWorkflowDrafts: vi.fn(),
  fetchWorkflowQueue: vi.fn(),
  updateWorkflow: vi.fn(),
  deleteWorkflow: vi.fn(),
  triggerWorkflow: vi.fn(),
}));

vi.mock("../../api/feeds", () => ({
  fetchFeeds: vi.fn(),
  retryFeedQueueItem: vi.fn(),
  deleteFeedQueueItem: vi.fn(),
}));

vi.mock("../../api/accounts", () => ({
  listAccounts: vi.fn(),
}));

describe("WorkflowDetailPage", () => {
  const routerFuture = { v7_startTransition: true, v7_relativeSplatPath: true };

  const mockWorkflow: workflowsApi.AutomationWorkflowDto = {
    id: "wf-1",
    title: "Daily Tech Highlights",
    instructionPrompt: "Summarize top 3 articles.",
    sources: [
      { feedSubscriptionId: "sub-1", title: "Hacker News", feedUrl: "https://news.ycombinator.com/rss" },
    ],
    destinations: [
      { id: "dest-1", platform: "Bluesky", autoPublish: true, accountHandle: "alice.bsky.social" },
    ],
    createdAt: "2026-08-20T10:00:00Z",
    updatedAt: "2026-08-20T10:00:00Z",
  };

  const mockDrafts: workflowsApi.WorkflowDraftSummaryDto[] = [
    {
      id: "draft-1",
      title: "Why .NET 10 is Exciting",
      status: "Formatting",
      targetPlatform: null,
      threads: [
        { platform: "Bluesky", stage: "Sent", accountHandle: "alice.bsky.social" }
      ],
      createdAt: "2026-08-21T10:00:00Z",
      updatedAt: "2026-08-21T10:00:00Z",
    },
    {
      id: "draft-2",
      title: "React 19 Server Actions",
      status: "Sourcing",
      targetPlatform: null,
      threads: [],
      createdAt: "2026-08-22T10:00:00Z",
      updatedAt: "2026-08-22T10:00:00Z",
    }
  ];

  beforeEach(() => {
    vi.clearAllMocks();

    vi.mocked(workflowsApi.fetchWorkflow).mockResolvedValue(mockWorkflow);
    vi.mocked(workflowsApi.fetchWorkflowDrafts).mockResolvedValue(mockDrafts);
    vi.mocked(workflowsApi.fetchWorkflowQueue).mockResolvedValue([]);
    vi.mocked(feedsApi.fetchFeeds).mockResolvedValue([]);
    vi.mocked(accountsApi.listAccounts).mockResolvedValue([]);
  });

  const renderComponent = (path = "/workflows/wf-1") => {
    return render(
      <MemoryRouter initialEntries={[path]} future={routerFuture}>
        <Routes>
          <Route path="/workflows/:id" element={<WorkflowDetailPage />} />
        </Routes>
      </MemoryRouter>
    );
  };

  it("renders workflow header and generated drafts list", async () => {
    renderComponent();

    await waitFor(() => {
      expect(screen.getByText("Daily Tech Highlights")).toBeInTheDocument();
      expect(screen.getByText(/1 feed source/i)).toBeInTheDocument();
      expect(screen.getAllByText("Bluesky").length).toBeGreaterThan(0);
      expect(screen.getByText("@alice.bsky.social")).toBeInTheDocument();
      expect(screen.getByText("Auto")).toBeInTheDocument();
    });

    // Verify drafts rendered
    expect(screen.getByText("Generated Drafts")).toBeInTheDocument();
    expect(screen.getByText("Why .NET 10 is Exciting")).toBeInTheDocument();
    expect(screen.getByText("React 19 Server Actions")).toBeInTheDocument();
    expect(screen.getByText("(Sent)")).toBeInTheDocument();
  });

  it("opens execution history modal on clicking Execution History", async () => {
    renderComponent();

    await waitFor(() => {
      expect(screen.getByText("Execution History")).toBeInTheDocument();
    });

    const historyBtn = screen.getByRole("button", { name: /Execution History/i });
    fireEvent.click(historyBtn);

    await waitFor(() => {
      expect(screen.getByTestId("execution-history-modal")).toBeInTheDocument();
    });
  });

  it("triggers workflow execution when clicking Trigger Now", async () => {
    vi.mocked(workflowsApi.triggerWorkflow).mockResolvedValue();

    renderComponent();

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /Trigger Now/i })).toBeInTheDocument();
    });

    const triggerBtn = screen.getByRole("button", { name: /Trigger Now/i });
    fireEvent.click(triggerBtn);

    await waitFor(() => {
      expect(workflowsApi.triggerWorkflow).toHaveBeenCalledWith("wf-1");
    });
  });

  it("renders empty state when no drafts are generated", async () => {
    vi.mocked(workflowsApi.fetchWorkflowDrafts).mockResolvedValue([]);

    renderComponent();

    await waitFor(() => {
      expect(screen.getByText("No drafts generated yet")).toBeInTheDocument();
      expect(screen.getByText("Trigger Ingestion Now")).toBeInTheDocument();
    });
  });

  it("opens edit modal when clicking edit button", async () => {
    renderComponent();

    await waitFor(() => {
      expect(screen.getByTitle("Edit workflow")).toBeInTheDocument();
    });

    const editBtn = screen.getByTitle("Edit workflow");
    fireEvent.click(editBtn);

    await waitFor(() => {
      expect(screen.getByRole("heading", { name: "Edit Automation Workflow" })).toBeInTheDocument();
    });
  });
});
