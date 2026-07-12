import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import React from "react";
import { CreateDraftModal } from "./CreateDraftModal";

describe("CreateDraftModal", () => {
  const mockOnClose = vi.fn();
  const mockOnCreate = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("does not render when isOpen is false", () => {
    render(<CreateDraftModal isOpen={false} onClose={mockOnClose} onCreate={mockOnCreate} />);
    expect(screen.queryByText("Create New Draft")).not.toBeInTheDocument();
  });

  it("renders the modal when isOpen is true", () => {
    render(<CreateDraftModal isOpen={true} onClose={mockOnClose} onCreate={mockOnCreate} />);
    expect(screen.getByText("Create New Draft")).toBeInTheDocument();
    expect(screen.getByPlaceholderText("Untitled")).toBeInTheDocument();
  });

  it("calls onClose when Cancel is clicked", () => {
    render(<CreateDraftModal isOpen={true} onClose={mockOnClose} onCreate={mockOnCreate} />);
    fireEvent.click(screen.getByRole("button", { name: /cancel/i }));
    expect(mockOnClose).toHaveBeenCalledTimes(1);
  });

  it("calls onCreate with title and selected platform", async () => {
    mockOnCreate.mockResolvedValueOnce(undefined);
    render(<CreateDraftModal isOpen={true} onClose={mockOnClose} onCreate={mockOnCreate} />);

    await userEvent.type(screen.getByPlaceholderText("Untitled"), "My Thread");
    fireEvent.click(screen.getByRole("button", { name: /twitter/i }));
    fireEvent.click(screen.getByRole("button", { name: /create draft/i }));

    await waitFor(() => expect(mockOnCreate).toHaveBeenCalledWith("My Thread", "Twitter"));
  });

  it("calls onClose after successful creation", async () => {
    mockOnCreate.mockResolvedValueOnce(undefined);
    render(<CreateDraftModal isOpen={true} onClose={mockOnClose} onCreate={mockOnCreate} />);

    fireEvent.click(screen.getByRole("button", { name: /create draft/i }));

    await waitFor(() => expect(mockOnClose).toHaveBeenCalled());
  });

  it("defaults to Bluesky platform", () => {
    render(<CreateDraftModal isOpen={true} onClose={mockOnClose} onCreate={mockOnCreate} />);

    const blueskyBtn = screen.getByRole("button", { name: /bluesky/i });
    expect(blueskyBtn.className).toContain("bg-accent");
  });

  it("selects a platform when clicked", () => {
    render(<CreateDraftModal isOpen={true} onClose={mockOnClose} onCreate={mockOnCreate} />);

    fireEvent.click(screen.getByRole("button", { name: /linkedin/i }));

    expect(screen.getByRole("button", { name: /linkedin/i }).className).toContain("bg-accent");
    expect(screen.getByRole("button", { name: /bluesky/i }).className).not.toContain("bg-accent");
  });

  it("calls onCreate with undefined title when title is empty", async () => {
    mockOnCreate.mockResolvedValueOnce(undefined);
    render(<CreateDraftModal isOpen={true} onClose={mockOnClose} onCreate={mockOnCreate} />);

    fireEvent.click(screen.getByRole("button", { name: /create draft/i }));

    await waitFor(() => expect(mockOnCreate).toHaveBeenCalledWith(undefined, "Bluesky"));
  });

  it("submits on Enter key in title input", async () => {
    mockOnCreate.mockResolvedValueOnce(undefined);
    render(<CreateDraftModal isOpen={true} onClose={mockOnClose} onCreate={mockOnCreate} />);

    const titleInput = screen.getByPlaceholderText("Untitled");
    await userEvent.type(titleInput, "My Draft{Enter}");

    await waitFor(() => expect(mockOnCreate).toHaveBeenCalledWith("My Draft", "Bluesky"));
  });
});
