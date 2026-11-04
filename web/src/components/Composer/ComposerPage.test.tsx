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
    setPanelMode: vi.fn(),
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

  it("displays persistent sidebar and Thread Preview by default while hiding chat and editor", () => {
    render(
      <MemoryRouter initialEntries={["/draft/draft-1"]}>
        <Routes>
          <Route path="/draft/:draftId" element={<ComposerPage />} />
        </Routes>
      </MemoryRouter>
    );

    expect(screen.getByTestId("composer-page")).toBeInTheDocument();
    expect(screen.getByTestId("draft-sidebar")).toBeInTheDocument();
    expect(screen.getByTestId("composer-preview-view")).toBeInTheDocument();
    expect(screen.getByTestId("thread-preview")).toBeInTheDocument();

    // Navigation buttons in persistent sidebar
    expect(screen.getByTestId("menu-view-preview")).toBeInTheDocument();
    expect(screen.getByTestId("menu-view-editor")).toBeInTheDocument();
    expect(screen.getByTestId("menu-view-chat")).toBeInTheDocument();

    // Chat and editor panels are hidden by default
    expect(screen.queryByTestId("chat-panel")).not.toBeInTheDocument();
    expect(screen.queryByTestId("editor-panel")).not.toBeInTheDocument();
  });

  it("switches between preview, editor, and chat views via sidebar navigation", () => {
    render(
      <MemoryRouter initialEntries={["/draft/draft-1"]}>
        <Routes>
          <Route path="/draft/:draftId" element={<ComposerPage />} />
        </Routes>
      </MemoryRouter>
    );

    // Switch to Markdown Editor via sidebar
    fireEvent.click(screen.getByTestId("menu-view-editor"));
    expect(screen.getByTestId("composer-editor-view")).toBeInTheDocument();
    expect(screen.getByTestId("editor-panel")).toBeInTheDocument();
    expect(screen.queryByTestId("composer-preview-view")).not.toBeInTheDocument();

    // Click back to preview
    fireEvent.click(screen.getByTestId("back-to-preview-btn"));
    expect(screen.getByTestId("composer-preview-view")).toBeInTheDocument();
    expect(screen.queryByTestId("editor-panel")).not.toBeInTheDocument();

    // Switch to Chat via sidebar
    fireEvent.click(screen.getByTestId("menu-view-chat"));
    expect(screen.getByTestId("composer-chat-view")).toBeInTheDocument();
    expect(screen.getByTestId("chat-panel")).toBeInTheDocument();
    expect(screen.queryByTestId("composer-preview-view")).not.toBeInTheDocument();

    // Return to preview via sidebar
    fireEvent.click(screen.getByTestId("menu-view-preview"));
    expect(screen.getByTestId("composer-preview-view")).toBeInTheDocument();
    expect(screen.queryByTestId("chat-panel")).not.toBeInTheDocument();
  });
});
