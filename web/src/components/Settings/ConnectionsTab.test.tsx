import { describe, it, expect, vi, beforeEach, afterEach, type Mock } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import React from "react";
import { ConnectionsTab } from "./ConnectionsTab";

vi.mock("../../api/accounts", () => ({
  listAccounts: vi.fn(),
  saveAccount: vi.fn(),
  deleteAccount: vi.fn(),
}));

import { listAccounts, saveAccount, deleteAccount } from "../../api/accounts";
const mockListAccounts = listAccounts as Mock;
const mockSaveAccount = saveAccount as Mock;
const mockDeleteAccount = deleteAccount as Mock;

const makeAccount = (id = "a1") => ({
  id,
  platform: "Bluesky",
  handle: "alice.bsky.social",
  status: "Active",
  createdAt: "2026-01-01",
  updatedAt: "2026-01-01",
});

describe("ConnectionsTab", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.stubGlobal("confirm", () => true);
    mockListAccounts.mockResolvedValue([]);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("lists accounts loaded on mount", async () => {
    mockListAccounts.mockResolvedValueOnce([makeAccount("a1")]);
    render(<ConnectionsTab />);

    expect(await screen.findByText(/alice\.bsky\.social/)).toBeInTheDocument();
  });

  it("shows error when loading accounts fails", async () => {
    mockListAccounts.mockRejectedValueOnce(new Error("Network error"));
    render(<ConnectionsTab />);

    expect(await screen.findByText("Network error")).toBeInTheDocument();
  });

  it("does not submit when required fields are empty", async () => {
    render(<ConnectionsTab />);
    await screen.findByText("Bluesky");

    fireEvent.submit(screen.getByRole("button", { name: /save connection/i }));

    await waitFor(() => expect(mockSaveAccount).not.toHaveBeenCalled());
  });

  it("saves account and refreshes list on valid submit", async () => {
    mockListAccounts
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([makeAccount("a2")]);
    mockSaveAccount.mockResolvedValueOnce(undefined);
    render(<ConnectionsTab />);
    await screen.findByText("Bluesky");

    await userEvent.type(screen.getByPlaceholderText("e.g. user.bsky.social"), "alice.bsky.social");
    await userEvent.type(screen.getByPlaceholderText("••••••••••••"), "app-pw");
    fireEvent.submit(screen.getByRole("button", { name: /save connection/i }));

    await waitFor(() => expect(mockSaveAccount).toHaveBeenCalledWith({
      platform: "Bluesky",
      handle: "alice.bsky.social",
      appPassword: "app-pw",
    }));
    expect(await screen.findByText(/alice\.bsky\.social/)).toBeInTheDocument();
  });

  it("deletes account and refreshes list", async () => {
    mockListAccounts
      .mockResolvedValueOnce([makeAccount("a1")])
      .mockResolvedValueOnce([]);
    mockDeleteAccount.mockResolvedValueOnce(undefined);
    render(<ConnectionsTab />);

    expect(await screen.findByText(/alice\.bsky\.social/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /remove/i }));

    await waitFor(() => expect(mockDeleteAccount).toHaveBeenCalledWith("a1"));
    await waitFor(() => expect(screen.queryByText(/alice\.bsky\.social/)).not.toBeInTheDocument());
  });

  it("shows error when saveAccount fails", async () => {
    mockSaveAccount.mockRejectedValueOnce(new Error("Failed to save connection"));
    render(<ConnectionsTab />);
    await screen.findByText("Bluesky");

    await userEvent.type(screen.getByPlaceholderText("e.g. user.bsky.social"), "x");
    await userEvent.type(screen.getByPlaceholderText("••••••••••••"), "y");
    fireEvent.submit(screen.getByRole("button", { name: /save connection/i }));

    expect(await screen.findByText("Failed to save connection")).toBeInTheDocument();
  });
});
