import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import React from "react";
import App from "./App";

vi.mock("./components/Settings/SettingsPage", () => ({
  SettingsPage: () => <div data-testid="settings-page">Settings Page Content</div>,
}));

vi.mock("./components/WorkflowsPanel/WorkflowsPage", () => ({
  WorkflowsPage: () => <div data-testid="workflows-panel">Workflows Panel Content</div>,
  WorkflowsPanel: () => <div data-testid="workflows-panel">Workflows Panel Content</div>,
}));

vi.mock("./components/Composer/ComposerPage", () => ({
  ComposerPage: () => <div data-testid="composer-page">Composer Page Content</div>,
}));

vi.mock("./api/chat", () => ({
  ChatProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

describe("App", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders the top bar navigation links including Settings", async () => {
    render(<App />);

    expect(screen.getByText(/social-worker/)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Workflows" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Drafts" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Templates" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Settings" })).toBeInTheDocument();
  });

  it("navigates to Settings page when clicking the Settings link", async () => {
    render(<App />);

    fireEvent.click(screen.getByRole("link", { name: "Settings" }));
    expect(await screen.findByTestId("settings-page")).toBeInTheDocument();
  });
});
