import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import React from "react";
import { WorkflowDetailHeader } from "./WorkflowDetailHeader";
import type { AutomationWorkflowDto } from "../../api/workflows";

describe("WorkflowDetailHeader", () => {
  const baseWorkflow: AutomationWorkflowDto = {
    id: "wf-1",
    title: "Space Physics Feed",
    instructionPrompt: "Summarize astrophysics papers.",
    sources: [
      { feedSubscriptionId: "sub-1", title: "arXiv", feedUrl: "https://arxiv.org/rss" },
      { feedSubscriptionId: "sub-2", title: "NASA", feedUrl: "https://nasa.gov/rss" }
    ],
    destinations: [],
    createdAt: "2026-09-01T10:00:00Z",
    updatedAt: "2026-09-01T10:00:00Z"
  };

  it("renders drafts only when no destinations are configured", () => {
    render(
      <WorkflowDetailHeader
        workflow={baseWorkflow}
        onTrigger={vi.fn()}
        onOpenHistory={vi.fn()}
        onOpenEdit={vi.fn()}
        onDelete={vi.fn()}
      />
    );

    expect(screen.getByText("Space Physics Feed")).toBeInTheDocument();
    expect(screen.getByText("2 feed sources")).toBeInTheDocument();
    expect(screen.getByText("Drafts only")).toBeInTheDocument();
  });

  it("renders scheduled badge with schedule name", () => {
    const workflow: AutomationWorkflowDto = {
      ...baseWorkflow,
      destinations: [
        {
          id: "dest-1",
          platform: "Bluesky",
          autoPublish: false,
          publishingMode: "Scheduled",
          scheduleId: "sched-1",
          scheduleName: "Main Social Schedule",
          accountHandle: "science.bsky.social"
        }
      ]
    };

    render(
      <WorkflowDetailHeader
        workflow={workflow}
        onTrigger={vi.fn()}
        onOpenHistory={vi.fn()}
        onOpenEdit={vi.fn()}
        onDelete={vi.fn()}
      />
    );

    expect(screen.getByText("Bluesky")).toBeInTheDocument();
    expect(screen.getByText("@science.bsky.social")).toBeInTheDocument();
    expect(screen.getByText("Scheduled (Main Social Schedule)")).toBeInTheDocument();
  });

  it("renders manual review badge as Outbox Review", () => {
    const workflow: AutomationWorkflowDto = {
      ...baseWorkflow,
      destinations: [
        {
          id: "dest-1",
          platform: "Twitter",
          autoPublish: false,
          publishingMode: "Manual",
          accountHandle: "sciencetweets"
        }
      ]
    };

    render(
      <WorkflowDetailHeader
        workflow={workflow}
        onTrigger={vi.fn()}
        onOpenHistory={vi.fn()}
        onOpenEdit={vi.fn()}
        onDelete={vi.fn()}
      />
    );

    expect(screen.getByText("Twitter")).toBeInTheDocument();
    expect(screen.getByText("@sciencetweets")).toBeInTheDocument();
    expect(screen.getByText("Outbox Review")).toBeInTheDocument();
  });

  it("renders auto-publish badge when autoPublish or Automatic mode is set", () => {
    const workflow: AutomationWorkflowDto = {
      ...baseWorkflow,
      destinations: [
        {
          id: "dest-1",
          platform: "Bluesky",
          autoPublish: true,
          publishingMode: "Automatic"
        }
      ]
    };

    render(
      <WorkflowDetailHeader
        workflow={workflow}
        onTrigger={vi.fn()}
        onOpenHistory={vi.fn()}
        onOpenEdit={vi.fn()}
        onDelete={vi.fn()}
      />
    );

    expect(screen.getByText("Bluesky")).toBeInTheDocument();
    expect(screen.getByText("Auto-Publish")).toBeInTheDocument();
  });

  it("invokes callbacks when action buttons are clicked", () => {
    const onTrigger = vi.fn();
    const onOpenHistory = vi.fn();
    const onOpenEdit = vi.fn();
    const onDelete = vi.fn();

    render(
      <WorkflowDetailHeader
        workflow={baseWorkflow}
        onTrigger={onTrigger}
        onOpenHistory={onOpenHistory}
        onOpenEdit={onOpenEdit}
        onDelete={onDelete}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: /Trigger Now/i }));
    expect(onTrigger).toHaveBeenCalledTimes(1);

    fireEvent.click(screen.getByRole("button", { name: /Execution History/i }));
    expect(onOpenHistory).toHaveBeenCalledTimes(1);

    fireEvent.click(screen.getByTitle("Edit workflow"));
    expect(onOpenEdit).toHaveBeenCalledTimes(1);

    fireEvent.click(screen.getByTitle("Delete workflow"));
    expect(onDelete).toHaveBeenCalledTimes(1);
  });
});
