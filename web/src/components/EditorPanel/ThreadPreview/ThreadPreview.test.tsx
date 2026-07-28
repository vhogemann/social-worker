import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import React from "react";
import { ThreadPreview } from "./ThreadPreview";
import { useDraftStore } from "../../../store/draftStore";

vi.mock("./PreviewCard", () => ({
  PreviewCard: ({ content, index, postUrl }: { content: string; index: number; postUrl?: string }) => (
    <div data-testid={`preview-card-${index}`}>
      <span>{content}</span>
      <span>{postUrl ?? "no-post-url"}</span>
    </div>
  ),
}));

describe("ThreadPreview", () => {
  beforeEach(() => {
    useDraftStore.setState({
      activeDraftId: "draft-1",
      drafts: [
        {
          id: "draft-1",
          title: "Thread draft",
          status: "Draft",
          content: "First segment\n---\nSecond segment",
          targetPlatform: "Bluesky",
          canonicalDraftId: null,
          threads: [
            {
              id: "thread-1",
              draftId: "draft-1",
              platform: "Bluesky",
              stage: "Draft",
              content: "First segment\n---\nSecond segment",
              posts: [
                { id: "post-1", platformThreadId: "thread-1", segmentIndex: 0, platform: "Bluesky", url: "https://example.com/1" },
                { id: "post-2", platformThreadId: "thread-1", segmentIndex: 1, platform: "Bluesky", url: "https://example.com/2" },
              ],
            },
          ],
          mediaAssets: [],
          createdAt: "2026-07-10T00:00:00Z",
          updatedAt: "2026-07-10T00:00:00Z",
        },
      ],
    });
  });

  it("renders a preview card for each split segment", () => {
    render(<ThreadPreview content={`First segment
---
Second segment`} />);

    expect(screen.getByTestId("thread-preview")).toBeInTheDocument();
    expect(screen.getByTestId("preview-card-0")).toHaveTextContent("First segment");
    expect(screen.getByTestId("preview-card-0")).toHaveTextContent("https://example.com/1");
    expect(screen.getByTestId("preview-card-1")).toHaveTextContent("Second segment");
    expect(screen.getByTestId("preview-card-1")).toHaveTextContent("https://example.com/2");
  });

  it("shows the empty state when there is no content", () => {
    render(<ThreadPreview content="   " />);

    expect(screen.getByText(/No content to preview/i)).toBeInTheDocument();
  });
});