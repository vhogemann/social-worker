import { describe, it, expect, vi, beforeEach, afterEach, type Mock } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import React from "react";
import { ConnectionsTab } from "./ConnectionsTab";

vi.mock("../../api/accounts", () => ({
  listAccounts: vi.fn(),
  saveAccount: vi.fn(),
  deleteAccount: vi.fn(),
  getLinkedInAuthUrl: vi.fn(),
}));

import { listAccounts, saveAccount, deleteAccount, getLinkedInAuthUrl } from "../../api/accounts";
const mockListAccounts = listAccounts as Mock;
const mockSaveAccount = saveAccount as Mock;
const mockDeleteAccount = deleteAccount as Mock;
const mockGetLinkedInAuthUrl = getLinkedInAuthUrl as Mock;

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

  it("opens modal and does not submit when required fields are empty", async () => {
    render(<ConnectionsTab />);
    
    // Open modal
    const addBtn = await screen.findByRole("button", { name: /\+? ?add connection/i });
    fireEvent.click(addBtn);

    expect(screen.getByText("Add Social Connection")).toBeInTheDocument();

    fireEvent.submit(screen.getByRole("button", { name: /save connection/i }));
    await waitFor(() => expect(mockSaveAccount).not.toHaveBeenCalled());
  });

  it("saves account and refreshes list on valid submit", async () => {
    mockListAccounts
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([makeAccount("a2")]);
    mockSaveAccount.mockResolvedValueOnce(undefined);
    render(<ConnectionsTab />);

    // Open modal
    const addBtn = await screen.findByRole("button", { name: /\+? ?add connection/i });
    fireEvent.click(addBtn);

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

    // Open modal
    const addBtn = await screen.findByRole("button", { name: /\+? ?add connection/i });
    fireEvent.click(addBtn);

    await userEvent.type(screen.getByPlaceholderText("e.g. user.bsky.social"), "x");
    await userEvent.type(screen.getByPlaceholderText("••••••••••••"), "y");
    fireEvent.submit(screen.getByRole("button", { name: /save connection/i }));

    expect(await screen.findByText("Failed to save connection")).toBeInTheDocument();
  });

  it("renders Company Page badge and Personal badge for LinkedIn accounts", async () => {
    mockListAccounts.mockResolvedValueOnce([
      {
        id: "li-1",
        platform: "LinkedIn",
        handle: "urn:li:person:user123",
        status: "Active",
        createdAt: "2026-01-01",
        updatedAt: "2026-01-01",
      },
      {
        id: "li-2",
        platform: "LinkedIn",
        handle: "Acme Corp (urn:li:organization:998877)",
        status: "Active",
        createdAt: "2026-01-01",
        updatedAt: "2026-01-01",
      },
    ]);
    render(<ConnectionsTab />);

    expect(await screen.findByText("Personal")).toBeInTheDocument();
    expect(await screen.findByText("Company Page")).toBeInTheDocument();
    expect(screen.getByText(/urn:li:person:user123/)).toBeInTheDocument();
    expect(screen.getByText(/Acme Corp/)).toBeInTheDocument();
  });

  it("initiates personal LinkedIn connection flow", async () => {
    mockGetLinkedInAuthUrl.mockResolvedValueOnce({ authUrl: "https://linkedin.com/auth?personal" });
    render(<ConnectionsTab />);

    const addBtn = await screen.findByRole("button", { name: /\+? ?add connection/i });
    fireEvent.click(addBtn);

    const select = screen.getByDisplayValue("Bluesky");
    fireEvent.change(select, { target: { value: "LinkedIn" } });

    const connectBtn = screen.getByRole("button", { name: /connect personal profile/i });
    fireEvent.click(connectBtn);

    await waitFor(() => {
      expect(mockGetLinkedInAuthUrl).toHaveBeenCalledWith("personal");
    });
  });

  it("initiates company LinkedIn connection flow when Company Page is selected", async () => {
    mockGetLinkedInAuthUrl.mockResolvedValueOnce({ authUrl: "https://linkedin.com/auth?company" });
    render(<ConnectionsTab />);

    const addBtn = await screen.findByRole("button", { name: /\+? ?add connection/i });
    fireEvent.click(addBtn);

    const select = screen.getByDisplayValue("Bluesky");
    fireEvent.change(select, { target: { value: "LinkedIn" } });

    const companyTypeBtn = screen.getByRole("button", { name: /company page/i });
    fireEvent.click(companyTypeBtn);

    const connectBtn = screen.getByRole("button", { name: /connect company page/i });
    fireEvent.click(connectBtn);

    await waitFor(() => {
      expect(mockGetLinkedInAuthUrl).toHaveBeenCalledWith("company");
    });
  });
});
