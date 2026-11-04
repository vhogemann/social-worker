import React from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { WorkflowSourcesSelector } from "./WorkflowSourcesSelector";
import { type FeedSubscriptionDto } from "../../api/feeds";
import { type AutomationWorkflowDto } from "../../api/workflows";

const mockFeeds: FeedSubscriptionDto[] = [
  {
    id: "feed-1",
    title: "TechCrunch",
    feedUrl: "https://techcrunch.com/feed/",
    websiteUrl: "https://techcrunch.com",
    lastPolledAt: null,
    createdAt: "2026-01-01T00:00:00Z"
  },
  {
    id: "feed-2",
    title: "Hacker News",
    feedUrl: "https://news.ycombinator.com/rss",
    websiteUrl: "https://news.ycombinator.com",
    lastPolledAt: null,
    createdAt: "2026-01-01T00:00:00Z"
  },
  {
    id: "feed-3",
    title: "The Verge",
    feedUrl: "https://theverge.com/rss/index.xml",
    websiteUrl: "https://theverge.com",
    lastPolledAt: null,
    createdAt: "2026-01-01T00:00:00Z"
  }
];

const mockWorkflows: AutomationWorkflowDto[] = [
  {
    id: "wf-1",
    title: "Source Pipeline",
    instructionPrompt: "Summarize",
    sources: [],
    destinations: [{ id: "d-1", platform: "Bluesky", autoPublish: false }],
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: "2026-01-01T00:00:00Z"
  }
];

describe("WorkflowSourcesSelector", () => {
  it("renders empty state when no feeds are selected", () => {
    render(
      <WorkflowSourcesSelector
        sourceKind="Feed"
        onSourceKindChange={vi.fn()}
        availableFeeds={mockFeeds}
        selectedFeedIds={[]}
        onToggleFeed={vi.fn()}
        availableWorkflows={[]}
        selectedSourceWorkflowIds={[]}
        onToggleSourceWorkflow={vi.fn()}
        lookbackDays={7}
        onLookbackDaysChange={vi.fn()}
      />
    );

    expect(screen.getByText(/no feeds selected for ingestion/i)).toBeInTheDocument();
    expect(screen.getByTestId("selected-feeds-count")).toHaveTextContent("0 feeds selected");
  });

  it("renders list of selected feeds with remove buttons", () => {
    const handleToggleFeed = vi.fn();

    render(
      <WorkflowSourcesSelector
        sourceKind="Feed"
        onSourceKindChange={vi.fn()}
        availableFeeds={mockFeeds}
        selectedFeedIds={["feed-1", "feed-2"]}
        onToggleFeed={handleToggleFeed}
        availableWorkflows={[]}
        selectedSourceWorkflowIds={[]}
        onToggleSourceWorkflow={vi.fn()}
        lookbackDays={7}
        onLookbackDaysChange={vi.fn()}
      />
    );

    expect(screen.getByText("TechCrunch")).toBeInTheDocument();
    expect(screen.getByText("Hacker News")).toBeInTheDocument();
    expect(screen.getByTestId("selected-feeds-count")).toHaveTextContent("2 feeds selected");

    const removeBtn = screen.getByTestId("remove-feed-feed-1");
    expect(removeBtn).toBeInTheDocument();
    fireEvent.click(removeBtn);

    expect(handleToggleFeed).toHaveBeenCalledWith("feed-1");
  });

  it("filters and adds feeds via autocomplete search", () => {
    const handleToggleFeed = vi.fn();

    render(
      <WorkflowSourcesSelector
        sourceKind="Feed"
        onSourceKindChange={vi.fn()}
        availableFeeds={mockFeeds}
        selectedFeedIds={["feed-1"]}
        onToggleFeed={handleToggleFeed}
        availableWorkflows={[]}
        selectedSourceWorkflowIds={[]}
        onToggleSourceWorkflow={vi.fn()}
        lookbackDays={7}
        onLookbackDaysChange={vi.fn()}
      />
    );

    const input = screen.getByTestId("feed-search-input");
    fireEvent.focus(input);
    fireEvent.change(input, { target: { value: "The Verge" } });
    fireEvent.keyDown(input, { key: "ArrowDown" });
    fireEvent.keyDown(input, { key: "Enter" });

    expect(handleToggleFeed).toHaveBeenCalledWith("feed-3");
  });

  it("shows helper message when all feeds are already selected", () => {
    render(
      <WorkflowSourcesSelector
        sourceKind="Feed"
        onSourceKindChange={vi.fn()}
        availableFeeds={mockFeeds}
        selectedFeedIds={["feed-1", "feed-2", "feed-3"]}
        onToggleFeed={vi.fn()}
        availableWorkflows={[]}
        selectedSourceWorkflowIds={[]}
        onToggleSourceWorkflow={vi.fn()}
        lookbackDays={7}
        onLookbackDaysChange={vi.fn()}
      />
    );

    expect(screen.getByText("0 available feeds ready to add.")).toBeInTheDocument();
    expect(screen.getByTestId("selected-feeds-count")).toHaveTextContent("3 feeds selected");
  });

  it("switches to Workflow Highlights and allows selecting source workflows", () => {
    const handleSourceKindChange = vi.fn();
    const handleToggleWorkflow = vi.fn();
    const handleLookbackChange = vi.fn();

    render(
      <WorkflowSourcesSelector
        sourceKind="WorkflowHighlights"
        onSourceKindChange={handleSourceKindChange}
        availableFeeds={mockFeeds}
        selectedFeedIds={[]}
        onToggleFeed={vi.fn()}
        availableWorkflows={mockWorkflows}
        selectedSourceWorkflowIds={[]}
        onToggleSourceWorkflow={handleToggleWorkflow}
        lookbackDays={14}
        onLookbackDaysChange={handleLookbackChange}
      />
    );

    expect(screen.getByLabelText(/lookback window/i)).toHaveValue(14);
    expect(screen.getByText("Source Pipeline")).toBeInTheDocument();

    fireEvent.click(screen.getByText("Source Pipeline"));
    expect(handleToggleWorkflow).toHaveBeenCalledWith("wf-1");

    fireEvent.change(screen.getByLabelText(/lookback window/i), { target: { value: "30" } });
    expect(handleLookbackChange).toHaveBeenCalledWith(30);
  });
});
