import React from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { DraftCard } from "./DraftCard";
import type { DraftDto } from "../../api/drafts";

const mockDraft: DraftDto = {
  id: "draft-1",
  title: "Announcement Thread",
  status: "Draft",
  content: "Exciting product update announcement!",
  targetPlatform: "Bluesky",
  canonicalDraftId: null,
  automationWorkflowId: "wf-1",
  automationWorkflowTitle: "Morning Tech Digest",
  threads: [],
  variants: [
    {
      id: "variant-1",
      title: "Announcement Thread - Twitter",
      status: "Draft",
      content: "Twitter variant",
      targetPlatform: "Twitter",
      canonicalDraftId: "draft-1",
      threads: [],
      createdAt: "2026-08-01T12:00:00Z",
      updatedAt: "2026-08-01T12:00:00Z"
    }
  ],
  createdAt: "2026-08-01T12:00:00Z",
  updatedAt: "2026-08-01T12:00:00Z"
};

describe("DraftCard", () => {
  it("renders draft details, platform badge, workflow pill, and variant counter", () => {
    const onSelect = vi.fn();
    render(
      <DraftCard
        draft={mockDraft}
        onSelect={onSelect}
        onArchive={vi.fn()}
        onUnarchive={vi.fn()}
        onDelete={vi.fn()}
        onRename={vi.fn()}
      />
    );

    expect(screen.getByText("Announcement Thread")).toBeInTheDocument();
    expect(screen.getByText("Exciting product update announcement!")).toBeInTheDocument();
    expect(screen.getByText("Morning Tech Digest")).toBeInTheDocument();
    expect(screen.getByText("1 variant")).toBeInTheDocument();
    expect(screen.getByText("Bluesky")).toBeInTheDocument();

    fireEvent.click(screen.getByTestId("draft-card"));
    expect(onSelect).toHaveBeenCalledWith("draft-1", "wf-1");
  });
});
