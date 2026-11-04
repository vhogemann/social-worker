import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { WorkflowEditorPage } from "./WorkflowEditorPage";
import * as workflowsApi from "../../api/workflows";
import * as feedsApi from "../../api/feeds";
import * as accountsApi from "../../api/accounts";
import * as schedulesApi from "../../api/schedules";

vi.mock("../../api/workflows");
vi.mock("../../api/feeds");
vi.mock("../../api/accounts");
vi.mock("../../api/schedules");

const mockWorkflow: workflowsApi.AutomationWorkflowDto = {
  id: "wf-1",
  title: "Morning Tech Digest",
  instructionPrompt: "Summarize top tech stories.",
  createdAt: "2026-08-01T12:00:00Z",
  updatedAt: "2026-08-01T12:00:00Z",
  minRelevanceScore: 7,
  sources: [
    {
      feedSubscriptionId: "feed-1",
      title: "Hacker News",
      feedUrl: "https://example.com/feed.xml"
    }
  ],
  destinations: [
    {
      id: "dest-1",
      platform: "Bluesky",
      autoPublish: true,
      accountHandle: "bot.bsky.social"
    }
  ]
};

describe("WorkflowEditorPage", () => {
  const routerFuture = { v7_startTransition: true, v7_relativeSplatPath: true };

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(feedsApi.fetchFeeds).mockResolvedValue([
      {
        id: "feed-1",
        title: "Hacker News",
        feedUrl: "https://example.com/feed.xml",
        websiteUrl: "https://example.com",
        lastPolledAt: null,
        createdAt: "2026-08-01T12:00:00Z"
      }
    ]);
    vi.mocked(accountsApi.listAccounts).mockResolvedValue([
      {
        id: "acc-1",
        platform: "Bluesky",
        handle: "alice.bsky.social",
        status: "Active",
        createdAt: "2026-08-01T12:00:00Z",
        updatedAt: "2026-08-01T12:00:00Z"
      }
    ]);
    vi.mocked(workflowsApi.fetchWorkflows).mockResolvedValue([]);
    vi.mocked(schedulesApi.fetchSchedules).mockResolvedValue([]);
  });

  it("renders new workflow page and creates workflow on submit", async () => {
    vi.mocked(workflowsApi.createWorkflow).mockResolvedValue({ ...mockWorkflow, id: "wf-new" });

    render(
      <MemoryRouter initialEntries={["/workflows/new"]} future={routerFuture}>
        <Routes>
          <Route path="/workflows/new" element={<WorkflowEditorPage />} />
        </Routes>
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByRole("heading", { name: "Create Automation Workflow" })).toBeInTheDocument();
    });

    const titleInput = screen.getByLabelText(/workflow title/i);
    fireEvent.change(titleInput, { target: { value: "Brand New Pipeline" } });

    const submitBtn = screen.getByRole("button", { name: /create workflow/i });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(workflowsApi.createWorkflow).toHaveBeenCalledWith(
        expect.objectContaining({
          title: "Brand New Pipeline"
        })
      );
    });
  });

  it("renders edit workflow page when id param is present", async () => {
    vi.mocked(workflowsApi.fetchWorkflow).mockResolvedValue(mockWorkflow);
    vi.mocked(workflowsApi.updateWorkflow).mockResolvedValue(mockWorkflow);

    render(
      <MemoryRouter initialEntries={["/workflows/wf-1/edit"]} future={routerFuture}>
        <Routes>
          <Route path="/workflows/:id/edit" element={<WorkflowEditorPage />} />
        </Routes>
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByRole("heading", { name: "Edit Automation Workflow" })).toBeInTheDocument();
      expect(screen.getByDisplayValue("Morning Tech Digest")).toBeInTheDocument();
    });

    const submitBtn = screen.getByRole("button", { name: /save changes/i });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(workflowsApi.updateWorkflow).toHaveBeenCalledWith(
        "wf-1",
        expect.objectContaining({
          title: "Morning Tech Digest"
        })
      );
    });
  });

  it("switches sidebar tabs to Quality & Filtering and updates relevance threshold", async () => {
    vi.mocked(workflowsApi.fetchWorkflow).mockResolvedValue(mockWorkflow);
    vi.mocked(workflowsApi.updateWorkflow).mockResolvedValue(mockWorkflow);

    render(
      <MemoryRouter initialEntries={["/workflows/wf-1/edit"]} future={routerFuture}>
        <Routes>
          <Route path="/workflows/:id/edit" element={<WorkflowEditorPage />} />
        </Routes>
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByRole("heading", { name: "Edit Automation Workflow" })).toBeInTheDocument();
    });

    // Click "Quality & Filtering" sidebar tab
    const filterTabBtn = screen.getByRole("button", { name: /quality & filtering/i });
    fireEvent.click(filterTabBtn);

    await waitFor(() => {
      expect(screen.getByTestId("relevance-score-badge")).toHaveTextContent("7/10");
    });

    // Change slider value to 9
    const slider = screen.getByTestId("relevance-score-slider");
    fireEvent.change(slider, { target: { value: "9" } });
    expect(screen.getByTestId("relevance-score-badge")).toHaveTextContent("9/10");

    const submitBtn2 = screen.getByRole("button", { name: /save changes/i });
    fireEvent.click(submitBtn2);

    await waitFor(() => {
      expect(workflowsApi.updateWorkflow).toHaveBeenCalledWith(
        "wf-1",
        expect.objectContaining({
          minRelevanceScore: 9
        })
      );
    });
  });

  it("switches sidebar tabs to Ingestion Sources and Destinations", async () => {
    vi.mocked(workflowsApi.fetchWorkflow).mockResolvedValue(mockWorkflow);

    render(
      <MemoryRouter initialEntries={["/workflows/wf-1/edit"]} future={routerFuture}>
        <Routes>
          <Route path="/workflows/:id/edit" element={<WorkflowEditorPage />} />
        </Routes>
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByRole("heading", { name: "Edit Automation Workflow" })).toBeInTheDocument();
    });

    // Switch to Ingestion Sources
    fireEvent.click(screen.getByRole("button", { name: /ingestion sources/i }));
    await waitFor(() => {
      expect(screen.getByText("Hacker News")).toBeInTheDocument();
    });

    // Switch to Destinations
    fireEvent.click(screen.getByRole("button", { name: /destinations/i }));
    await waitFor(() => {
      expect(screen.getByTestId("toggle-dest-bluesky")).toBeInTheDocument();
    });
  });
});
