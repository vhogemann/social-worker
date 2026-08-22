import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import React from "react";
import App from "./App";
import { useDraftStore } from "./store/draftStore";

vi.mock("./components/Settings/SettingsPage", () => ({
  SettingsPage: () => <div data-testid="settings-page">Settings Page Content</div>,
}));

vi.mock("./components/WorkflowsPanel/WorkflowsPanel", () => ({
  WorkflowsPanel: () => <div data-testid="workflows-panel">Workflows Panel Content</div>,
}));

vi.mock("./components/ChatPanel/ChatPanel", () => ({
  ChatPanel: () => <div data-testid="chat-panel" />,
}));

vi.mock("./components/EditorPanel/EditorPanel", () => ({
  EditorPanel: () => <div data-testid="editor-panel" />,
}));

vi.mock("./components/DraftList/DraftList", () => ({
  DraftList: () => <div data-testid="draft-list" />,
}));

vi.mock("./api/chat", () => ({
  ChatProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

describe("App", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useDraftStore.setState({
      drafts: [
        {
          id: "draft-1",
          title: "Test Draft",
          content: "Test content",
          status: "Draft",
          targetPlatform: null,
          canonicalDraftId: null,
          threads: [],
          createdAt: "2026-08-22T10:00:00Z",
          updatedAt: "2026-08-22T10:00:00Z",
        },
      ],
      activeDraftId: "draft-1",
      loadDrafts: vi.fn().mockResolvedValue(undefined),
    });
  });

  it("renders the top bar navigation links including Settings", async () => {
    render(<App />);

    await waitFor(() => {
      expect(screen.getByText(/social-worker/)).toBeInTheDocument();
    });

    expect(screen.getByRole("link", { name: "Workflows" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Composer" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Sources Library" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Feeds" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Logs" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Privacy" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Settings" })).toBeInTheDocument();
  });

  it("navigates to Settings page when clicking the Settings link", async () => {
    render(<App />);

    await waitFor(() => {
      expect(screen.getByRole("link", { name: "Settings" })).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole("link", { name: "Settings" }));
    expect(await screen.findByTestId("settings-page")).toBeInTheDocument();
  });
});
