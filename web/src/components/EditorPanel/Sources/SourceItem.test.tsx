import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import React from "react";
import { SourceItem } from "./SourceItem";

describe("SourceItem", () => {
  const onInsert = vi.fn();
  const onPreview = vi.fn();
  const onDelete = vi.fn();

  it("renders the source details and invokes row actions", () => {
    const source = {
      id: "source-1",
      draftId: "draft-1",
      kind: "Url",
      reference: "https://example.com",
      title: "Example Source",
      summary: "Summary",
      processingStatus: "Complete",
      youtubeVideoId: null,
      addedAt: "2026-07-10T00:00:00Z",
    };

    render(<SourceItem source={source} onInsert={onInsert} onPreview={onPreview} onDelete={onDelete} />);

    expect(screen.getByText("Example Source")).toBeInTheDocument();
    expect(screen.getByText("Complete")).toBeInTheDocument();

    fireEvent.click(screen.getByTitle("Insert link into editor"));
    fireEvent.click(screen.getByTitle("Preview source content"));
    fireEvent.click(screen.getByTitle("Remove link reference from draft"));

    expect(onInsert).toHaveBeenCalledWith(source);
    expect(onPreview).toHaveBeenCalledWith(source);
    expect(onDelete).toHaveBeenCalledWith(source);
  });
});