import { describe, expect, it, vi, beforeEach } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import React from "react";
import { MediaAssetItem } from "./MediaAssetItem";

describe("MediaAssetItem", () => {
  const onInsert = vi.fn();
  const onPreview = vi.fn();
  const onDelete = vi.fn();
  const clipboardWriteText = vi.fn().mockResolvedValue(undefined);

  beforeEach(() => {
    vi.clearAllMocks();
    Object.assign(navigator, {
      clipboard: {
        writeText: clipboardWriteText,
      },
    });
  });

  it("renders the asset details and invokes row actions", () => {
    const asset = {
      id: "asset-1",
      draftId: "draft-1",
      fileName: "image.png",
      mimeType: "image/png",
      altText: "Alt text",
      filePath: "uploads/image.png",
      sizeBytes: 4096,
      width: 800,
      height: 600,
      createdAt: "2026-07-10T00:00:00Z",
    };

    render(<MediaAssetItem asset={asset} onInsert={onInsert} onPreview={onPreview} onDelete={onDelete} />);

    expect(screen.getByText("image.png")).toBeInTheDocument();
    expect(screen.getByText(/media:\/\/asset-1/)).toBeInTheDocument();

    fireEvent.click(screen.getByTitle("Insert image into editor"));
    fireEvent.click(screen.getByTitle("Preview image"));
    fireEvent.click(screen.getByTitle("Copy markdown tag"));
    fireEvent.click(screen.getByTitle("Delete attached image"));

    expect(onInsert).toHaveBeenCalledWith(asset);
    expect(onPreview).toHaveBeenCalledWith(asset);
    expect(onDelete).toHaveBeenCalledWith("asset-1");
    expect(clipboardWriteText).toHaveBeenCalledWith("![image.png](media://asset-1)");
  });
});