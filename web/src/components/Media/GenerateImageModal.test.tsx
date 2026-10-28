import { describe, expect, it, vi, beforeEach } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import React from "react";
import { GenerateImageModal } from "./GenerateImageModal";

const mockGenerateMediaImage = vi.fn();
const mockFetchTemplates = vi.fn();

vi.mock("../../store/draftStore", () => ({
  useDraftStore: (selector: any) =>
    selector({
      generateMediaImage: mockGenerateMediaImage,
    }),
}));

vi.mock("../../api/templates", () => ({
  fetchTemplates: () => mockFetchTemplates(),
}));

describe("GenerateImageModal", () => {
  const onClose = vi.fn();
  const onInserted = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    mockFetchTemplates.mockResolvedValue([
      {
        id: "tpl-1",
        name: "Test Template",
        width: 1080,
        height: 1080,
        schema: JSON.stringify({
          slots: [{ name: "headline", type: "text", defaultValue: "Default Headline" }],
        }),
      },
    ]);
  });

  it("does not render when isOpen is false", () => {
    render(
      <GenerateImageModal
        isOpen={false}
        onClose={onClose}
        draftId="draft-123"
        onInserted={onInserted}
      />
    );

    expect(screen.queryByTestId("generate-image-modal")).not.toBeInTheDocument();
  });

  it("renders form fields and fetches templates when open", async () => {
    render(
      <GenerateImageModal
        isOpen={true}
        onClose={onClose}
        draftId="draft-123"
        onInserted={onInserted}
      />
    );

    expect(screen.getByTestId("generate-image-modal")).toBeInTheDocument();
    expect(screen.getByPlaceholderText(/E.g. A futuristic glass workstation/i)).toBeInTheDocument();

    await waitFor(() => {
      expect(mockFetchTemplates).toHaveBeenCalled();
      expect(screen.getByText(/Test Template/i)).toBeInTheDocument();
    });
  });

  it("calls generateMediaImage and inserts into draft on submit", async () => {
    mockGenerateMediaImage.mockResolvedValue({
      mediaAssetId: "asset-gen-1",
      markdownTag: "![Cyberpunk](media://asset-gen-1)",
      url: "/api/media/asset-gen-1",
    });

    render(
      <GenerateImageModal
        isOpen={true}
        onClose={onClose}
        draftId="draft-123"
        onInserted={onInserted}
      />
    );

    const promptInput = screen.getByPlaceholderText(/E.g. A futuristic glass workstation/i);
    fireEvent.change(promptInput, { target: { value: "Cyberpunk cityscape" } });

    const generateBtn = screen.getByRole("button", { name: "Generate" });
    fireEvent.click(generateBtn);

    await waitFor(() => {
      expect(mockGenerateMediaImage).toHaveBeenCalledWith({
        draftId: "draft-123",
        prompt: "Cyberpunk cityscape",
        aspectRatio: "1:1",
        model: "black-forest-labs/flux-3-image-klein",
        templateId: undefined,
        templateSlots: undefined,
        altText: undefined,
      });
      expect(screen.getByRole("button", { name: "Insert into Draft" })).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole("button", { name: "Insert into Draft" }));
    expect(onInserted).toHaveBeenCalledWith("![Cyberpunk](media://asset-gen-1)");
    expect(onClose).toHaveBeenCalled();
  });
});
