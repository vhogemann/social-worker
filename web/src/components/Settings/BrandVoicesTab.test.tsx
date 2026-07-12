import { describe, it, expect, vi, beforeEach, afterEach, type Mock } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import React from "react";
import { BrandVoicesTab } from "./BrandVoicesTab";

vi.mock("../../api/brandVoices", () => ({
  listBrandVoices: vi.fn(),
  createBrandVoice: vi.fn(),
  updateBrandVoice: vi.fn(),
  deleteBrandVoice: vi.fn(),
}));

import { listBrandVoices, createBrandVoice, updateBrandVoice, deleteBrandVoice } from "../../api/brandVoices";
const mockListBrandVoices = listBrandVoices as Mock;
const mockCreateBrandVoice = createBrandVoice as Mock;
const mockUpdateBrandVoice = updateBrandVoice as Mock;
const mockDeleteBrandVoice = deleteBrandVoice as Mock;

const makeVoice = (id = "bv1", name = "Tech Writer", isDefault = true) => ({
  id,
  name,
  body: "Write concise technical content.",
  isDefault,
  createdAt: "2026-01-01",
  updatedAt: "2026-01-01",
});

describe("BrandVoicesTab", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.stubGlobal("confirm", () => true);
    mockListBrandVoices.mockResolvedValue([]);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("lists brand voices loaded on mount", async () => {
    mockListBrandVoices.mockResolvedValueOnce([makeVoice()]);
    render(<BrandVoicesTab />);

    expect(await screen.findByText("Tech Writer")).toBeInTheDocument();
  });

  it("shows error when loading fails", async () => {
    mockListBrandVoices.mockRejectedValueOnce(new Error("Failed to load brand voices"));
    render(<BrandVoicesTab />);

    expect(await screen.findByText("Failed to load brand voices")).toBeInTheDocument();
  });

  it("shows add form when clicking Add button", async () => {
    render(<BrandVoicesTab />);
    await screen.findByRole("button", { name: /new voice/i });

    fireEvent.click(screen.getByRole("button", { name: /new voice/i }));

    expect(screen.getByPlaceholderText("e.g. Friendly & Informal")).toBeInTheDocument();
  });

  it("creates a brand voice and refreshes list", async () => {
    const newVoice = makeVoice("bv2", "Casual");
    mockListBrandVoices
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([newVoice]);
    mockCreateBrandVoice.mockResolvedValueOnce(newVoice);
    render(<BrandVoicesTab />);

    await screen.findByRole("button", { name: /new voice/i });
    fireEvent.click(screen.getByRole("button", { name: /new voice/i }));

    await userEvent.type(screen.getByPlaceholderText("e.g. Friendly & Informal"), "Casual");
    await userEvent.type(screen.getByPlaceholderText(/write in a relaxed/i), "Write casually.");

    fireEvent.click(screen.getByRole("button", { name: /save brand voice/i }));

    await waitFor(() => expect(mockCreateBrandVoice).toHaveBeenCalledWith(
      expect.objectContaining({ name: "Casual", body: "Write casually." })
    ));
    expect(await screen.findByText("Casual")).toBeInTheDocument();
  });

  it("deletes a brand voice and refreshes list", async () => {
    mockListBrandVoices
      .mockResolvedValueOnce([makeVoice()])
      .mockResolvedValueOnce([]);
    mockDeleteBrandVoice.mockResolvedValueOnce(undefined);
    render(<BrandVoicesTab />);

    await screen.findByText("Tech Writer");
    fireEvent.click(screen.getByRole("button", { name: "Delete" }));

    await waitFor(() => expect(mockDeleteBrandVoice).toHaveBeenCalledWith("bv1"));
    await waitFor(() => expect(screen.queryByText("Tech Writer")).not.toBeInTheDocument());
  });

  it("updates default status when clicking set-default button", async () => {
    const voices = [makeVoice("bv1", "Tech Writer", false), makeVoice("bv2", "Casual", false)];
    mockListBrandVoices
      .mockResolvedValueOnce(voices)
      .mockResolvedValueOnce([makeVoice("bv1", "Tech Writer", true), makeVoice("bv2", "Casual", false)]);
    mockUpdateBrandVoice.mockResolvedValueOnce(undefined);
    render(<BrandVoicesTab />);

    await screen.findByText("Casual");
    fireEvent.click(screen.getAllByRole("button", { name: "Set Default" })[0]);

    await waitFor(() => expect(mockUpdateBrandVoice).toHaveBeenCalledWith("bv1", expect.objectContaining({ isDefault: true })));
  });
});
