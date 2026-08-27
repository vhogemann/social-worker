import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import React from "react";
import { MemoryRouter } from "react-router-dom";
import { WorkflowsPanel } from "./WorkflowsPanel";
import * as workflowsApi from "../../api/workflows";
import * as feedsApi from "../../api/feeds";
import * as accountsApi from "../../api/accounts";

vi.mock("../../api/workflows", () => ({
  fetchWorkflows: vi.fn(),
  createWorkflow: vi.fn(),
  updateWorkflow: vi.fn(),
  deleteWorkflow: vi.fn(),
  triggerWorkflow: vi.fn(),
}));

vi.mock("../../api/feeds", () => ({
  fetchFeeds: vi.fn(),
}));

vi.mock("../../api/accounts", () => ({
  listAccounts: vi.fn(),
}));

describe("WorkflowsPanel", () => {
  const routerFuture = { v7_startTransition: true, v7_relativeSplatPath: true };

  beforeEach(() => {
    vi.clearAllMocks();

    vi.mocked(workflowsApi.fetchWorkflows).mockResolvedValue([
      {
        id: "wf-1",
        title: "Daily Tech Highlights",
        instructionPrompt: "Summarize top 3 articles.",
        sources: [
          { feedSubscriptionId: "sub-1", title: "Hacker News", feedUrl: "https://news.ycombinator.com/rss" },
        ],
        destinations: [
          { id: "dest-1", platform: "Bluesky", autoPublish: true },
        ],
        createdAt: "2026-08-20T10:00:00Z",
        updatedAt: "2026-08-20T10:00:00Z",
      },
    ]);

    vi.mocked(feedsApi.fetchFeeds).mockResolvedValue([
      {
        id: "sub-1",
        title: "Hacker News",
        feedUrl: "https://news.ycombinator.com/rss",
        createdAt: "2026-08-20T09:00:00Z",
      },
    ]);

    vi.mocked(accountsApi.listAccounts).mockResolvedValue([
      {
        id: "acc-1",
        platform: "Bluesky",
        handle: "alice.bsky.social",
        status: "Active",
        createdAt: "2026-08-20T08:00:00Z",
        updatedAt: "2026-08-20T08:00:00Z",
      },
    ]);
  });

  it("renders workflow cards, sources, and destination badges", async () => {
    render(
      <MemoryRouter future={routerFuture}>
        <WorkflowsPanel />
      </MemoryRouter>
    );

    expect(screen.getByText("Automation Workflows")).toBeInTheDocument();

    await waitFor(() => {
      expect(screen.getByText("Daily Tech Highlights")).toBeInTheDocument();
      expect(screen.getByText("Hacker News")).toBeInTheDocument();
      expect(screen.getByText("Bluesky")).toBeInTheDocument();
      expect(screen.getByText("Auto-Publish")).toBeInTheDocument();
    });
  });

  it("opens create modal when clicking New Workflow", async () => {
    render(
      <MemoryRouter future={routerFuture}>
        <WorkflowsPanel />
      </MemoryRouter>
    );

    const newBtn = screen.getByRole("button", { name: /New Workflow/i });
    fireEvent.click(newBtn);

    await waitFor(() => {
      expect(screen.getByRole("heading", { name: "Create Automation Workflow" })).toBeInTheDocument();
      expect(screen.getByPlaceholderText(/e\.g\. Daily Tech Highlights/i)).toBeInTheDocument();
    });
  });

  it("navigates to workflow detail on card click", async () => {
    render(
      <MemoryRouter future={routerFuture}>
        <WorkflowsPanel />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText("Daily Tech Highlights")).toBeInTheDocument();
    });

    const cardTitle = screen.getByText("Daily Tech Highlights");
    fireEvent.click(cardTitle);
  });
});
