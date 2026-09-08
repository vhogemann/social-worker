import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import React from "react";
import { MemoryRouter } from "react-router-dom";
import { EditorPanel } from "./EditorPanel";
import { useDraftStore } from "../../store/draftStore";

const mockSetPanelMode = vi.fn();
const mockPublish = vi.fn();

const mockDraft = {
  id: "draft-1",
  title: "Reply draft",
  status: "Editing",
  content: "Draft content",
  targetPlatform: "Bluesky",
  canonicalDraftId: null,
  threads: [
    {
      id: "thread-1",
      draftId: "draft-1",
      platform: "Bluesky",
      stage: "Draft",
      content: "Draft content",
      posts: [] as any[],
    },
  ],
  mediaAssets: [],
  blueskyReplyTarget: {
    replyRootUri: "at://did:plc:root/app.bsky.feed.post/1",
    replyRootCid: "root-cid",
    replyParentUri: "at://did:plc:parent/app.bsky.feed.post/2",
    replyParentCid: "parent-cid",
    replyParentUrl: "https://bsky.app/profile/example/post/2",
    replyParentAuthor: "example.bsky.social",
    replyParentText: "Parent post preview text.",
    replyParentAvatarUrl: "https://cdn.bsky.app/avatar.jpg",
  },
  createdAt: "2026-07-16T00:00:00Z",
  updatedAt: "2026-07-16T00:00:00Z",
};

let mockCurrentDraft = mockDraft;

vi.mock("../Composer/ComposerDraftContext", () => ({
  useComposerDraft: () => ({
    doc: "Draft content",
    panelMode: "edit",
    setPanelMode: mockSetPanelMode,
    activeDraft: mockCurrentDraft,
    activeDraftId: "draft-1",
    canonicalDraftId: "draft-1",
    publish: mockPublish,
  }),
}));

vi.mock("./MarkdownEditor", () => ({
  MarkdownEditor: () => <div data-testid="markdown-editor" />,
}));

vi.mock("./ThreadPreview", () => ({
  ThreadPreview: () => <div data-testid="thread-preview" />,
}));

vi.mock("./Sources/SourcesPanel", () => ({
  SourcesPanel: () => <div data-testid="sources-panel" />,
}));

vi.mock("./AdaptVariantsModal", () => ({
  AdaptVariantsModal: () => null,
}));

describe("EditorPanel reply target card", () => {
  beforeEach(() => {
    mockCurrentDraft = mockDraft;
    mockSetPanelMode.mockClear();
    mockPublish.mockReset();

    useDraftStore.setState({
      activeDraftId: "draft-1",
      platformCapabilities: [{ platform: "Bluesky", supportsReplyTarget: true }],
      drafts: [mockDraft],
      supportsReplyTargetForPlatform: vi.fn().mockReturnValue(true),
      loadPlatformCapabilities: vi.fn().mockResolvedValue(undefined),
    });
  });

  it("renders reply target card with author and preview text", () => {
    render(
      <MemoryRouter>
        <EditorPanel />
      </MemoryRouter>
    );

    expect(screen.getByTestId("reply-target-card")).toBeInTheDocument();
    expect(screen.getByText("example.bsky.social")).toBeInTheDocument();
    expect(screen.getByText("Parent post preview text.")).toBeInTheDocument();
  });

  it("renders open-original-post link to canonical URL", () => {
    render(
      <MemoryRouter>
        <EditorPanel />
      </MemoryRouter>
    );

    const link = screen.getByTestId("reply-target-open-link");
    expect(link).toHaveAttribute("href", "https://bsky.app/profile/example/post/2");
    expect(link).toHaveAttribute("target", "_blank");
  });

  it("displays publish validation failures in error banner", async () => {
    mockPublish.mockRejectedValue(new Error("Post exceeds 300 character limit."));

    render(
      <MemoryRouter>
        <EditorPanel />
      </MemoryRouter>
    );

    fireEvent.click(screen.getByRole("button", { name: "Publish" }));

    await waitFor(() => {
      expect(screen.getByTestId("publish-error-banner")).toBeInTheDocument();
      expect(screen.getByText("Post exceeds 300 character limit.")).toBeInTheDocument();
    });
  });

  it("renders published badge and view live post link when draft is published", () => {
    mockCurrentDraft = {
      ...mockDraft,
      threads: [
        {
          id: "thread-1",
          draftId: "draft-1",
          platform: "Bluesky",
          stage: "Sent",
          content: "Draft content",
          posts: [
            {
              id: "post-1",
              platformThreadId: "thread-1",
              segmentIndex: 0,
              platform: "Bluesky",
              url: "https://bsky.app/profile/example/post/100",
            },
          ],
        },
      ],
    };

    render(
      <MemoryRouter>
        <EditorPanel />
      </MemoryRouter>
    );

    expect(screen.getByTestId("published-badge")).toHaveTextContent("Published");
    const link = screen.getByTestId("published-post-link");
    expect(link).toHaveAttribute("href", "https://bsky.app/profile/example/post/100");
    expect(link).toHaveTextContent("View on Bluesky");
    expect(screen.queryByRole("button", { name: "Publish" })).not.toBeInTheDocument();
  });
});
