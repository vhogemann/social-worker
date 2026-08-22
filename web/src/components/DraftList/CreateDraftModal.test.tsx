import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import React from "react";
import { MemoryRouter } from "react-router-dom";
import { CreateDraftModal } from "./CreateDraftModal";
import * as accountsApi from "../../api/accounts";

vi.mock("../../api/accounts", () => ({
  listAccounts: vi.fn(),
}));

function renderModal(props: { isOpen: boolean; onClose: () => void; onCreate: (title?: string, platform?: string, accountId?: string) => Promise<void> }) {
  return render(
    <MemoryRouter>
      <CreateDraftModal {...props} />
    </MemoryRouter>
  );
}

describe("CreateDraftModal", () => {
  const mockOnClose = vi.fn();
  const mockOnCreate = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(accountsApi.listAccounts).mockResolvedValue([
      { id: "acc-1", platform: "Bluesky", handle: "user.bsky.social", status: "Active", createdAt: "", updatedAt: "" },
      { id: "acc-2", platform: "Twitter", handle: "user", status: "Active", createdAt: "", updatedAt: "" },
      { id: "acc-3", platform: "LinkedIn", handle: "user-li", status: "Active", createdAt: "", updatedAt: "" },
    ]);
  });

  it("does not render when isOpen is false", () => {
    renderModal({ isOpen: false, onClose: mockOnClose, onCreate: mockOnCreate });
    expect(screen.queryByText("Create New Draft")).not.toBeInTheDocument();
  });

  it("renders the modal and configured accounts when isOpen is true", async () => {
    renderModal({ isOpen: true, onClose: mockOnClose, onCreate: mockOnCreate });

    expect(screen.getByText("Create New Draft")).toBeInTheDocument();
    expect(screen.getByPlaceholderText("Untitled")).toBeInTheDocument();

    await waitFor(() => {
      expect(screen.getByText(/@user\.bsky\.social/i)).toBeInTheDocument();
      expect(screen.getByText(/@user-li/i)).toBeInTheDocument();
      expect(screen.getByRole("button", { name: /bluesky/i })).toBeInTheDocument();
      expect(screen.getByRole("button", { name: /twitter/i })).toBeInTheDocument();
      expect(screen.getByRole("button", { name: /linkedin/i })).toBeInTheDocument();
    });
  });

  it("defaults to Bluesky when Bluesky account is configured", async () => {
    renderModal({ isOpen: true, onClose: mockOnClose, onCreate: mockOnCreate });

    await waitFor(() => {
      const blueskyBtn = screen.getByRole("button", { name: /bluesky/i });
      expect(blueskyBtn.className).toContain("border-accent");
    });
  });

  it("defaults to the first configured account when Bluesky is not configured", async () => {
    vi.mocked(accountsApi.listAccounts).mockResolvedValue([
      { id: "acc-1", platform: "LinkedIn", handle: "user-li", status: "Active", createdAt: "", updatedAt: "" },
      { id: "acc-2", platform: "Twitter", handle: "user", status: "Active", createdAt: "", updatedAt: "" },
    ]);

    renderModal({ isOpen: true, onClose: mockOnClose, onCreate: mockOnCreate });

    await waitFor(() => {
      const linkedinBtn = screen.getByRole("button", { name: /linkedin/i });
      expect(linkedinBtn.className).toContain("border-accent");
      expect(screen.queryByRole("button", { name: /bluesky/i })).not.toBeInTheDocument();
    });
  });

  it("displays empty state with link to connections when no accounts are configured", async () => {
    vi.mocked(accountsApi.listAccounts).mockResolvedValue([]);

    renderModal({ isOpen: true, onClose: mockOnClose, onCreate: mockOnCreate });

    await waitFor(() => {
      expect(screen.getByText(/no social accounts configured/i)).toBeInTheDocument();
      expect(screen.getByRole("link", { name: /configure connections/i })).toHaveAttribute("href", "/settings/connections");
    });

    const createBtn = screen.getByRole("button", { name: /create draft/i });
    expect(createBtn).toBeDisabled();
  });

  it("calls onClose when Cancel is clicked", () => {
    renderModal({ isOpen: true, onClose: mockOnClose, onCreate: mockOnCreate });
    fireEvent.click(screen.getByRole("button", { name: /cancel/i }));
    expect(mockOnClose).toHaveBeenCalledTimes(1);
  });

  it("calls onCreate with title and selected account", async () => {
    mockOnCreate.mockResolvedValueOnce(undefined);
    renderModal({ isOpen: true, onClose: mockOnClose, onCreate: mockOnCreate });

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /twitter/i })).toBeInTheDocument();
    });

    await userEvent.type(screen.getByPlaceholderText("Untitled"), "My Thread");
    fireEvent.click(screen.getByRole("button", { name: /twitter/i }));
    fireEvent.click(screen.getByRole("button", { name: /create draft/i }));

    await waitFor(() => expect(mockOnCreate).toHaveBeenCalledWith("My Thread", "Twitter", "acc-2"));
  });

  it("calls onClose after successful creation", async () => {
    mockOnCreate.mockResolvedValueOnce(undefined);
    renderModal({ isOpen: true, onClose: mockOnClose, onCreate: mockOnCreate });

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /bluesky/i })).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole("button", { name: /create draft/i }));

    await waitFor(() => expect(mockOnClose).toHaveBeenCalled());
  });

  it("submits on Enter key in title input", async () => {
    mockOnCreate.mockResolvedValueOnce(undefined);
    renderModal({ isOpen: true, onClose: mockOnClose, onCreate: mockOnCreate });

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /bluesky/i })).toBeInTheDocument();
    });

    const titleInput = screen.getByPlaceholderText("Untitled");
    await userEvent.type(titleInput, "My Draft{Enter}");

    await waitFor(() => expect(mockOnCreate).toHaveBeenCalledWith("My Draft", "Bluesky", "acc-1"));
  });
});
