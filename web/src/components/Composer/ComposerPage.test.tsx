import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { ComposerPage } from "./ComposerPage";

vi.mock("./ComposerDraftContext", () => ({
  ComposerDraftProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  useComposerDraft: () => ({
    activeDraft: {
      id: "draft-1",
      title: "Test Draft",
      content: "Content",
      targetPlatform: "Bluesky",
      threads: [
        {
          id: "thread-1",
          platform: "Bluesky",
          stage: "Draft",
          content: "Content",
          posts: [],
        },
      ],
      variants: [],
    },
    canonicalDraft: {
      id: "draft-1",
      title: "Test Draft",
      content: "Content",
      targetPlatform: "Bluesky",
      threads: [],
      variants: [],
    },
    activeDraftId: "draft-1",
    canonicalDraftId: "draft-1",
    workflowId: undefined,
    doc: "Content",
    isLoading: false,
    publish: vi.fn().mockResolvedValue({ success: true }),
  }),
}));

vi.mock("../../api/chat", () => ({
  ScopedChatProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

vi.mock("../ChatPanel/ChatPanel", () => ({
  ChatPanel: () => <div data-testid="chat-panel">Chat Panel</div>,
}));

vi.mock("../EditorPanel/EditorPanel", () => ({
  EditorPanel: () => <div data-testid="editor-panel">Editor Panel</div>,
}));

vi.mock("../EditorPanel/ThreadPreview", () => ({
  ThreadPreview: ({ content }: { content: string }) => (
    <div data-testid="thread-preview">Thread Preview: {content}</div>
  ),
}));

describe("ComposerPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("displays only Thread Preview by default and hides chat and editor", () => {
    render(
      <MemoryRouter initialEntries={["/draft/draft-1"]}>
        <Routes>
          <Route path="/draft/:draftId" element={<ComposerPage />} />
        </Routes>
      </MemoryRouter>
    );

    expect(screen.getByTestId("composer-page")).toBeInTheDocument();
    expect(screen.getByTestId("composer-preview-view")).toBeInTheDocument();
    expect(screen.getByTestId("thread-preview")).toBeInTheDocument();
    expect(screen.getByTestId("draft-hamburger-btn")).toBeInTheDocument();

    // Chat and editor are hidden by default
    expect(screen.queryByTestId("chat-panel")).not.toBeInTheDocument();
    expect(screen.queryByTestId("editor-panel")).not.toBeInTheDocument();
    expect(screen.queryByTestId("draft-menu-drawer")).not.toBeInTheDocument();
  });

  it("opens hamburger drawer and switches between preview, editor, and chat", () => {
    render(
      <MemoryRouter initialEntries={["/draft/draft-1"]}>
        <Routes>
          <Route path="/draft/:draftId" element={<ComposerPage />} />
        </Routes>
      </MemoryRouter>
    );

    // Open hamburger menu
    fireEvent.click(screen.getByTestId("draft-hamburger-btn"));
    expect(screen.getByTestId("draft-menu-drawer")).toBeInTheDocument();

    // Switch to Markdown Editor
    fireEvent.click(screen.getByTestId("menu-view-editor"));
    expect(screen.queryByTestId("draft-menu-drawer")).not.toBeInTheDocument();
    expect(screen.getByTestId("composer-editor-view")).toBeInTheDocument();
    expect(screen.getByTestId("editor-panel")).toBeInTheDocument();
    expect(screen.queryByTestId("composer-preview-view")).not.toBeInTheDocument();

    // Click back to preview
    fireEvent.click(screen.getByTestId("back-to-preview-btn"));
    expect(screen.getByTestId("composer-preview-view")).toBeInTheDocument();
    expect(screen.queryByTestId("editor-panel")).not.toBeInTheDocument();

    // Open hamburger menu again and switch to Chat
    fireEvent.click(screen.getByTestId("draft-hamburger-btn"));
    expect(screen.getByTestId("draft-menu-drawer")).toBeInTheDocument();

    fireEvent.click(screen.getByTestId("menu-view-chat"));
    expect(screen.queryByTestId("draft-menu-drawer")).not.toBeInTheDocument();
    expect(screen.getByTestId("composer-chat-view")).toBeInTheDocument();
    expect(screen.getByTestId("chat-panel")).toBeInTheDocument();
    expect(screen.queryByTestId("composer-preview-view")).not.toBeInTheDocument();

    // Return to preview
    fireEvent.click(screen.getByTestId("back-to-preview-btn"));
    expect(screen.getByTestId("composer-preview-view")).toBeInTheDocument();
  });
});
