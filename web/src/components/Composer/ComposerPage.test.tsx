import React from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { ComposerPage } from "./ComposerPage";

vi.mock("./ComposerDraftContext", () => ({
  ComposerDraftProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  useComposerDraft: () => ({
    activeDraft: { id: "draft-1", title: "Test Draft", content: "Content", threads: [], variants: [] },
    canonicalDraft: { id: "draft-1", title: "Test Draft", content: "Content", threads: [], variants: [] },
    activeDraftId: "draft-1",
    canonicalDraftId: "draft-1",
    workflowId: undefined,
    doc: "Content",
    isLoading: false,
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

describe("ComposerPage", () => {
  it("renders ChatPanel and EditorPanel in a full-width resizable layout without sidebar", () => {
    render(
      <MemoryRouter initialEntries={["/draft/draft-1"]}>
        <Routes>
          <Route path="/draft/:draftId" element={<ComposerPage />} />
        </Routes>
      </MemoryRouter>
    );

    expect(screen.getByTestId("composer-page")).toBeInTheDocument();
    expect(screen.queryByTestId("draft-list")).not.toBeInTheDocument();
    expect(screen.getByTestId("chat-panel")).toBeInTheDocument();
    expect(screen.getByTestId("editor-panel")).toBeInTheDocument();
  });
});
