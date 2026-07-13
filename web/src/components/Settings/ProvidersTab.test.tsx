import { describe, it, expect, vi, beforeEach, afterEach, type Mock } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import React from "react";
import { ProvidersTab } from "./ProvidersTab";

vi.mock("../../api/providers", () => ({
  listProviders: vi.fn(),
  createProvider: vi.fn(),
  updateProvider: vi.fn(),
  deleteProvider: vi.fn(),
  testProvider: vi.fn(),
}));

import { listProviders, createProvider, updateProvider, deleteProvider, testProvider } from "../../api/providers";
const mockListProviders = listProviders as Mock;
const mockCreateProvider = createProvider as Mock;
const mockDeleteProvider = deleteProvider as Mock;
const mockTestProvider = testProvider as Mock;

const makeProvider = (id = "p1") => ({
  id,
  name: "My Provider",
  providerType: "OpenRouter",
  baseUrl: "https://openrouter.ai/api/v1",
  apiKeySet: true,
  model: "claude-3-5-sonnet",
  contextWindowTokens: 131072,
  isDefault: true,
  isActive: true,
  supportsVision: true,
  supportsTools: true,
});

describe("ProvidersTab", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.stubGlobal("confirm", () => true);
    mockListProviders.mockResolvedValue([]);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("lists providers loaded on mount", async () => {
    mockListProviders.mockResolvedValueOnce([makeProvider()]);
    render(<ProvidersTab />);

    expect(await screen.findByText("My Provider")).toBeInTheDocument();
  });

  it("shows error when loading fails", async () => {
    mockListProviders.mockRejectedValueOnce(new Error("Failed to load providers"));
    render(<ProvidersTab />);

    expect(await screen.findByText("Failed to load providers")).toBeInTheDocument();
  });

  it("shows validation error when submitting form with missing required fields", async () => {
    render(<ProvidersTab />);
    await screen.findByRole("button", { name: /add provider/i });

    fireEvent.click(screen.getByRole("button", { name: /add provider/i }));

    expect(await screen.findByText(/name, base url, and model are required/i)).toBeInTheDocument();
    expect(mockCreateProvider).not.toHaveBeenCalled();
  });

  it("creates a provider and refreshes list", async () => {
    const newProvider = makeProvider("p2");
    mockListProviders
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([newProvider]);
    mockCreateProvider.mockResolvedValueOnce(newProvider);
    render(<ProvidersTab />);
    await screen.findByRole("button", { name: /add provider/i });

    await userEvent.type(screen.getByPlaceholderText("e.g. OpenRouter Claude"), "My Provider");
    await userEvent.type(screen.getByPlaceholderText("e.g. anthropic/claude-3.5-sonnet"), "claude-3-5-sonnet");

    fireEvent.click(screen.getByRole("button", { name: /add provider/i }));
    expect(await screen.findByText("My Provider")).toBeInTheDocument();
  });

  it("shows success message when test connection succeeds", async () => {
    mockTestProvider.mockResolvedValueOnce({ success: true, error: null, contextWindowTokens: 131072 });
    render(<ProvidersTab />);
    await screen.findByRole("button", { name: /test connection/i });

    await userEvent.type(screen.getByPlaceholderText("e.g. anthropic/claude-3.5-sonnet"), "claude-3-5-sonnet");
    fireEvent.click(screen.getByRole("button", { name: /test connection/i }));

    expect(await screen.findByText(/Connection successful!/)).toBeInTheDocument();
  });

  it("shows failure message when test connection fails", async () => {
    mockTestProvider.mockResolvedValueOnce({ success: false, error: "Invalid API key" });
    render(<ProvidersTab />);
    await screen.findByRole("button", { name: /test connection/i });

    await userEvent.type(screen.getByPlaceholderText("e.g. anthropic/claude-3.5-sonnet"), "any-model");
    fireEvent.click(screen.getByRole("button", { name: /test connection/i }));

    expect(await screen.findByText("Invalid API key")).toBeInTheDocument();
  });
});
