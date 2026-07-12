import { describe, it, expect, vi, beforeEach, type Mock } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import React from "react";
import { AuthGuard } from "./AuthGuard";
import { useAuthStore } from "../../store/authStore";

const mockInitialize = vi.fn();

function resetStore(overrides: Partial<{ initialized: boolean; isAuthenticated: boolean }> = {}) {
  useAuthStore.setState({
    initialized: false,
    isAuthenticated: false,
    initialize: mockInitialize,
    ...overrides,
  } as any);
}

describe("AuthGuard", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockInitialize.mockResolvedValue(undefined);
    resetStore();
  });

  it("shows loading spinner when not initialized", () => {
    resetStore({ initialized: false, isAuthenticated: false });
    render(<AuthGuard><div>App Content</div></AuthGuard>);

    expect(screen.getByText(/loading session/i)).toBeInTheDocument();
    expect(screen.queryByText("App Content")).not.toBeInTheDocument();
  });

  it("renders LoginPage when initialized and not authenticated", () => {
    resetStore({ initialized: true, isAuthenticated: false });
    render(<AuthGuard><div>App Content</div></AuthGuard>);

    expect(screen.getByPlaceholderText("Email or username")).toBeInTheDocument();
    expect(screen.queryByText("App Content")).not.toBeInTheDocument();
  });

  it("renders children when initialized and authenticated", () => {
    resetStore({ initialized: true, isAuthenticated: true });
    render(<AuthGuard><div>App Content</div></AuthGuard>);

    expect(screen.getByText("App Content")).toBeInTheDocument();
    expect(screen.queryByPlaceholderText("Email or username")).not.toBeInTheDocument();
  });

  it("calls initialize on mount", () => {
    resetStore({ initialized: false });
    render(<AuthGuard><div>App</div></AuthGuard>);
    expect(mockInitialize).toHaveBeenCalledTimes(1);
  });

  it("renders children after initialization completes", async () => {
    mockInitialize.mockImplementation(() => {
      useAuthStore.setState({ initialized: true, isAuthenticated: true });
      return Promise.resolve();
    });
    resetStore({ initialized: false });
    render(<AuthGuard><div>App Content</div></AuthGuard>);

    await waitFor(() => expect(screen.getByText("App Content")).toBeInTheDocument());
  });
});
