import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import React from "react";
import { EditorPanel } from "./EditorPanel";
import { useDraftStore } from "../../store/draftStore";
import { useEditorStore } from "../../store/editorStore";
import { useChatStore } from "../../store/chatStore";

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
    useEditorStore.setState({
      doc: "Draft content",
      panelMode: "edit",
      setPanelMode: vi.fn(),
    });

    useDraftStore.setState({
      activeDraftId: "draft-1",
      platformCapabilities: [{ platform: "Bluesky", supportsReplyTarget: true }],
      drafts: [
        {
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
        },
      ],
      sources: [],
      loading: false,
      loadDrafts: vi.fn(),
      loadPlatformCapabilities: vi.fn().mockResolvedValue(undefined),
      supportsReplyTargetForPlatform: vi.fn().mockReturnValue(true),
      createDraft: vi.fn(),
      switchDraft: vi.fn(),
      updateDraftTitle: vi.fn(),
      saveDraftContent: vi.fn(),
      archiveDraft: vi.fn(),
      unarchiveDraft: vi.fn(),
      deleteDraft: vi.fn(),
      setActivePlatform: vi.fn(),
      createThreadVariant: vi.fn(),
      updateThreadStage: vi.fn(),
      updateThreadContent: vi.fn(),
      publishThread: vi.fn(),
      loadSources: vi.fn(),
      uploadFileSource: vi.fn(),
      uploadMediaAsset: vi.fn(),
      updateMediaAltText: vi.fn(),
      deleteMediaAsset: vi.fn(),
      saveDraftChat: vi.fn(),
    });

    useChatStore.setState({
      messagesByDraft: {},
      activityCardsByDraft: {},
      saveMessages: vi.fn(),
      loadMessages: vi.fn().mockReturnValue(null),
      clearMessages: vi.fn(),
      addActivityCard: useChatStore.getState().addActivityCard,
      clearActivityCards: vi.fn(),
    });
  });

  it("renders reply target card with author and preview text", () => {
    render(<EditorPanel />);

    expect(screen.getByTestId("reply-target-card")).toBeInTheDocument();
    expect(screen.getByText("example.bsky.social")).toBeInTheDocument();
    expect(screen.getByText("Parent post preview text.")).toBeInTheDocument();
  });

  it("renders open-original-post link to canonical URL", () => {
    render(<EditorPanel />);

    const link = screen.getByTestId("reply-target-open-link");
    expect(link).toHaveAttribute("href", "https://bsky.app/profile/example/post/2");
    expect(link).toHaveAttribute("target", "_blank");
  });

  it("adds publish validation failures as chat activity cards instead of alert", async () => {
    const alertSpy = vi.spyOn(window, "alert").mockImplementation(() => {});
    const publishThread = vi.fn().mockRejectedValue(new Error("Post exceeds 300 character limit."));
    useDraftStore.setState({ publishThread });

    render(<EditorPanel />);

    fireEvent.click(screen.getByRole("button", { name: "Publish" }));

    await waitFor(() => {
      const cards = useChatStore.getState().activityCardsByDraft["draft-1"];
      expect(cards).toBeDefined();
      expect(cards[cards.length - 1].title).toBe("publish failed");
      expect(cards[cards.length - 1].message).toBe("Post exceeds 300 character limit.");
    });
    expect(alertSpy).not.toHaveBeenCalled();
    alertSpy.mockRestore();
  });
});
