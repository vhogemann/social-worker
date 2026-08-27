import React from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { SourceGridCard } from "./SourceGridCard";
import { type SourceSearchItemDto } from "../../api/drafts";

const mockItem: SourceSearchItemDto = {
  id: "src-1",
  kind: "Url",
  reference: "https://example.com/article",
  title: "Building Resilient Systems",
  summary: "A practical guide to distributed systems.",
  processingStatus: "Processed",
  youtubeVideoId: null,
  addedAt: "2026-08-01T12:00:00Z"
};

describe("SourceGridCard", () => {
  it("renders source metadata and triggers preview & link actions", () => {
    const handleOpenPreview = vi.fn();
    const handleLinkSource = vi.fn();

    render(
      <SourceGridCard
        item={mockItem}
        isLinked={false}
        isLinking={false}
        hasActiveDraft={true}
        onOpenPreview={handleOpenPreview}
        onLinkSource={handleLinkSource}
      />
    );

    expect(screen.getByText("Building Resilient Systems")).toBeInTheDocument();
    expect(screen.getByText("https://example.com/article")).toBeInTheDocument();
    expect(screen.getByText("Url")).toBeInTheDocument();

    const linkBtn = screen.getByRole("button", { name: /link to draft/i });
    fireEvent.click(linkBtn);
    expect(handleLinkSource).toHaveBeenCalledWith("src-1");

    fireEvent.click(screen.getByText("Building Resilient Systems"));
    expect(handleOpenPreview).toHaveBeenCalledWith(mockItem);
  });
});
