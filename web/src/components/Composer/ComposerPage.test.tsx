import React from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { ComposerPage } from "./ComposerPage";

vi.mock("./useComposerDraftSync", () => ({
  useComposerDraftSync: () => ({ isReady: true }),
}));

vi.mock("../ChatPanel/ChatPanel", () => ({
  ChatPanel: () => <div data-testid="chat-panel">Chat Panel</div>,
}));

vi.mock("../EditorPanel/EditorPanel", () => ({
  EditorPanel: () => <div data-testid="editor-panel">Editor Panel</div>,
}));

describe("ComposerPage", () => {
  it("renders ChatPanel and EditorPanel in a full-width resizable layout without sidebar", () => {
    render(<ComposerPage />);

    expect(screen.getByTestId("composer-page")).toBeInTheDocument();
    expect(screen.queryByTestId("draft-list")).not.toBeInTheDocument();
    expect(screen.getByTestId("chat-panel")).toBeInTheDocument();
    expect(screen.getByTestId("editor-panel")).toBeInTheDocument();
  });
});
