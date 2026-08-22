import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import React from "react";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { SettingsPage } from "./SettingsPage";
import { useAuthStore } from "../../store/authStore";

vi.mock("./AccountTab", () => ({
  AccountTab: () => <div data-testid="account-tab">Account Tab Content</div>,
}));

vi.mock("./ConnectionsTab", () => ({
  ConnectionsTab: () => <div data-testid="connections-tab">Connections Tab Content</div>,
}));

vi.mock("./BrandVoicesTab", () => ({
  BrandVoicesTab: () => <div data-testid="brand-voices-tab">Brand Voices Tab Content</div>,
}));

vi.mock("./UsersTab", () => ({
  UsersTab: () => <div data-testid="users-tab">Users Tab Content</div>,
}));

vi.mock("./ProvidersTab", () => ({
  ProvidersTab: () => <div data-testid="providers-tab">Providers Tab Content</div>,
}));

function renderSettingsPage(initialEntries: string[] = ["/settings/account"]) {
  return render(
    <MemoryRouter initialEntries={initialEntries}>
      <Routes>
        <Route path="/settings/:tab" element={<SettingsPage />} />
        <Route path="/settings" element={<SettingsPage />} />
        <Route path="/login" element={<div data-testid="login-page">Login Page</div>} />
      </Routes>
    </MemoryRouter>
  );
}

describe("SettingsPage", () => {
  const logoutMock = vi.fn().mockResolvedValue(undefined);

  beforeEach(() => {
    vi.clearAllMocks();
    useAuthStore.setState({
      user: { id: "user-1", username: "demouser", email: "demo@example.com", role: "Admin" },
      accessToken: "mock-token",
      refreshToken: "mock-refresh-token",
      logout: logoutMock,
    });
  });

  it("renders the account tab by default with sidebar navigation", () => {
    renderSettingsPage(["/settings/account"]);

    expect(screen.getByRole("heading", { name: "Account Settings" })).toBeInTheDocument();
    expect(screen.getByTestId("account-tab")).toBeInTheDocument();
    expect(screen.getByText("Signed In As")).toBeInTheDocument();
    expect(screen.getByText("demouser")).toBeInTheDocument();

    // General group
    expect(screen.getByRole("link", { name: /account/i })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /connections/i })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /brand voices/i })).toBeInTheDocument();

    // Admin group
    expect(screen.getByRole("link", { name: /users/i })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /llm providers/i })).toBeInTheDocument();
  });

  it("renders connections tab when navigated to /settings/connections", () => {
    renderSettingsPage(["/settings/connections"]);

    expect(screen.getByRole("heading", { name: "Social Connections" })).toBeInTheDocument();
    expect(screen.getByTestId("connections-tab")).toBeInTheDocument();
  });

  it("renders brand voices tab when navigated to /settings/brand-voices", () => {
    renderSettingsPage(["/settings/brand-voices"]);

    expect(screen.getByRole("heading", { name: "Brand Voice Prompts" })).toBeInTheDocument();
    expect(screen.getByTestId("brand-voices-tab")).toBeInTheDocument();
  });

  it("renders users tab for admin users", () => {
    renderSettingsPage(["/settings/users"]);

    expect(screen.getByRole("heading", { name: "User Management" })).toBeInTheDocument();
    expect(screen.getByTestId("users-tab")).toBeInTheDocument();
  });

  it("renders providers tab for admin users", () => {
    renderSettingsPage(["/settings/providers"]);

    expect(screen.getByRole("heading", { name: "LLM Provider Configuration" })).toBeInTheDocument();
    expect(screen.getByTestId("providers-tab")).toBeInTheDocument();
  });

  it("hides admin tabs for non-admin users", () => {
    useAuthStore.setState({
      user: { id: "user-2", username: "standarduser", email: "std@example.com", role: "User" },
      accessToken: "mock-token",
      refreshToken: "mock-refresh-token",
      logout: logoutMock,
    });

    renderSettingsPage(["/settings/account"]);

    expect(screen.queryByText("Admin")).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /users/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /llm providers/i })).not.toBeInTheDocument();
  });

  it("redirects non-admin users to account when trying to access admin tab", () => {
    useAuthStore.setState({
      user: { id: "user-2", username: "standarduser", email: "std@example.com", role: "User" },
      accessToken: "mock-token",
      refreshToken: "mock-refresh-token",
      logout: logoutMock,
    });

    renderSettingsPage(["/settings/providers"]);

    expect(screen.getByRole("heading", { name: "Account Settings" })).toBeInTheDocument();
    expect(screen.getByTestId("account-tab")).toBeInTheDocument();
  });

  it("handles sign out correctly", async () => {
    renderSettingsPage(["/settings/account"]);

    const signOutBtn = screen.getByRole("button", { name: /sign out/i });
    fireEvent.click(signOutBtn);

    expect(logoutMock).toHaveBeenCalledTimes(1);
    expect(await screen.findByTestId("login-page")).toBeInTheDocument();
  });
});
