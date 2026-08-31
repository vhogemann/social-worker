import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import React from "react";
import { MemoryRouter } from "react-router-dom";
import { EditorPanel } from "./EditorPanel";
import { useDraftStore } from "../../store/draftStore";

const mockAddActivityCard = vi.fn();
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
      posts: [],
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

vi.mock("../Composer/ComposerDraftContext", () => ({
  useComposerDraft: () => ({
    doc: "Draft content",
    panelMode: "edit",
    setPanelMode: mockSetPanelMode,
    activeDraft: mockDraft,
    activeDraftId: "draft-1",
    canonicalDraftId: "draft-1",
    addActivityCard: mockAddActivityCard,
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
    mockAddActivityCard.mockClear();
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

  it("adds publish validation failures as chat activity cards instead of alert", async () => {
    const alertSpy = vi.spyOn(window, "alert").mockImplementation(() => {});
    mockPublish.mockRejectedValue(new Error("Post exceeds 300 character limit."));

    render(
      <MemoryRouter>
        <EditorPanel />
      </MemoryRouter>
    );

    fireEvent.click(screen.getByRole("button", { name: "Publish" }));

    await waitFor(() => {
      expect(mockAddActivityCard).toHaveBeenCalledWith({
        title: "publish failed",
        message: "Post exceeds 300 character limit.",
        kind: "error",
      });
    });
    expect(alertSpy).not.toHaveBeenCalled();
    alertSpy.mockRestore();
  });
});
