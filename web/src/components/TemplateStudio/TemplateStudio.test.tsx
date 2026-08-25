import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import React from "react";
import { MemoryRouter } from "react-router-dom";
import { TemplateStudio } from "./TemplateStudio";
import * as templatesApi from "../../api/templates";

vi.mock("../../api/templates", () => ({
  fetchTemplates: vi.fn(),
  fetchTemplate: vi.fn(),
  createTemplate: vi.fn(),
  updateTemplate: vi.fn(),
  deleteTemplate: vi.fn(),
  generateTemplate: vi.fn(),
  previewTemplate: vi.fn(),
  restoreDefaultTemplates: vi.fn(),
}));

describe("TemplateStudio", () => {
  const routerFuture = { v7_startTransition: true, v7_relativeSplatPath: true };

  beforeEach(() => {
    vi.clearAllMocks();

    vi.mocked(templatesApi.fetchTemplates).mockResolvedValue([
      {
        id: "t-1",
        name: "Quote Card (Square)",
        description: "Modern square 1:1 quote card",
        width: 1080,
        height: 1080,
        schema: JSON.stringify({
          name: "Quote Card (Square)",
          width: 1080,
          height: 1080,
          slots: [{ name: "quote_text", type: "text" }, { name: "author", type: "text" }],
          layers: [],
        }),
        thumbnailMediaAssetId: null,
        thumbnailUrl: "/api/templates/t-1/render",
        createdAt: "2026-08-25T10:00:00Z",
        updatedAt: "2026-08-25T10:00:00Z",
      },
      {
        id: "t-2",
        name: "Stat Highlight",
        description: "High impact metric highlight card",
        width: 1080,
        height: 1080,
        schema: JSON.stringify({
          name: "Stat Highlight",
          width: 1080,
          height: 1080,
          slots: [{ name: "metric_value", type: "text" }],
          layers: [],
        }),
        thumbnailMediaAssetId: null,
        thumbnailUrl: "/api/templates/t-2/render",
        createdAt: "2026-08-25T10:00:00Z",
        updatedAt: "2026-08-25T10:00:00Z",
      },
    ]);
  });

  it("renders template studio header and template cards", async () => {
    render(
      <MemoryRouter future={routerFuture}>
        <TemplateStudio />
      </MemoryRouter>
    );

    expect(screen.getByText("Template Studio")).toBeInTheDocument();

    await waitFor(() => {
      expect(screen.getByText("Quote Card (Square)")).toBeInTheDocument();
      expect(screen.getByText("Stat Highlight")).toBeInTheDocument();
      expect(screen.getByText("quote_text")).toBeInTheDocument();
      expect(screen.getByText("author")).toBeInTheDocument();
      expect(screen.getByText("metric_value")).toBeInTheDocument();
    });
  });

  it("opens AI Generator modal when clicking AI Generator button", async () => {
    render(
      <MemoryRouter future={routerFuture}>
        <TemplateStudio />
      </MemoryRouter>
    );

    const aiBtn = screen.getByRole("button", { name: /AI Generator/i });
    fireEvent.click(aiBtn);

    await waitFor(() => {
      expect(screen.getByText(/Generate Template with AI/i)).toBeInTheDocument();
      expect(screen.getByPlaceholderText(/Modern dark quote card with neon blue accents/i)).toBeInTheDocument();
    });
  });

  it("opens Template Editor modal when clicking + New Template", async () => {
    render(
      <MemoryRouter future={routerFuture}>
        <TemplateStudio />
      </MemoryRouter>
    );

    const newBtn = screen.getByRole("button", { name: /\+ New Template/i });
    fireEvent.click(newBtn);

    await waitFor(() => {
      expect(screen.getByText("Template Studio Editor")).toBeInTheDocument();
      expect(screen.getByPlaceholderText(/e\.g\. Modern Quote Card/i)).toBeInTheDocument();
    });
  });
});
