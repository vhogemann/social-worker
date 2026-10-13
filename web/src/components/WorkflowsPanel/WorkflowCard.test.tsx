import React from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { WorkflowCard } from "./WorkflowCard";
import { type AutomationWorkflowDto } from "../../api/workflows";

const mockWorkflow: AutomationWorkflowDto = {
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

describe("WorkflowCard", () => {
  it("renders workflow details and fires actions", () => {
    const handleClick = vi.fn();
    const handleTrigger = vi.fn();
    const handleEdit = vi.fn();
    const handleDelete = vi.fn();

    render(
      <WorkflowCard
        workflow={mockWorkflow}
        onClick={handleClick}
        onTrigger={handleTrigger}
        onEdit={handleEdit}
        onDelete={handleDelete}
      />
    );

    expect(screen.getByText("Morning Tech Digest")).toBeInTheDocument();
    expect(screen.getByText(/Summarize top tech stories/i)).toBeInTheDocument();
    expect(screen.getByText("Hacker News")).toBeInTheDocument();
    expect(screen.getByText("@bot.bsky.social")).toBeInTheDocument();
    expect(screen.getByText("Auto-Publish")).toBeInTheDocument();
    expect(screen.getByTestId("workflow-card-filter-badge")).toHaveTextContent("Filter: >= 7/10");

    fireEvent.click(screen.getByRole("button", { name: /trigger now/i }));
    expect(handleTrigger).toHaveBeenCalledWith("wf-1");

    fireEvent.click(screen.getByLabelText(/edit workflow/i));
    expect(handleEdit).toHaveBeenCalledWith(mockWorkflow);

    fireEvent.click(screen.getByLabelText(/delete workflow/i));
    expect(handleDelete).toHaveBeenCalledWith("wf-1");
  });
});
