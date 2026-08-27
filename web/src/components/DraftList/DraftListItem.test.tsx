import React from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { DraftListItem } from "./DraftListItem";
import type { DraftDto } from "../../api/drafts";

const mockDraft: DraftDto = {
  id: "draft-1",
  title: "My Super Draft",
  content: "Draft content",
  status: "Draft",
  targetPlatform: null,
  canonicalDraftId: null,
  threads: [],
  createdAt: "2026-08-01T12:00:00Z",
  updatedAt: "2026-08-01T12:00:00Z"
};

describe("DraftListItem", () => {
  it("renders draft title and selects on click", () => {
    const onSelect = vi.fn();
    render(
      <DraftListItem
        draft={mockDraft}
        isActive={false}
        isEditing={false}
        editTitleValue=""
        onSelect={onSelect}
        onStartEditTitle={vi.fn()}
        onChangeEditTitle={vi.fn()}
        onSaveTitle={vi.fn()}
        onCancelEditTitle={vi.fn()}
        onArchive={vi.fn()}
        onUnarchive={vi.fn()}
        onDelete={vi.fn()}
      />
    );

    expect(screen.getByText("My Super Draft")).toBeInTheDocument();
    fireEvent.click(screen.getByText("My Super Draft"));
    expect(onSelect).toHaveBeenCalledWith("draft-1");
  });

  it("renders input field when isEditing is true", () => {
    const onChange = vi.fn();
    const onSave = vi.fn();
    render(
      <DraftListItem
        draft={mockDraft}
        isActive={true}
        isEditing={true}
        editTitleValue="Editing Title"
        onSelect={vi.fn()}
        onStartEditTitle={vi.fn()}
        onChangeEditTitle={onChange}
        onSaveTitle={onSave}
        onCancelEditTitle={vi.fn()}
        onArchive={vi.fn()}
        onUnarchive={vi.fn()}
        onDelete={vi.fn()}
      />
    );

    const input = screen.getByRole("textbox");
    expect(input).toHaveValue("Editing Title");
    fireEvent.change(input, { target: { value: "New Title" } });
    expect(onChange).toHaveBeenCalledWith("New Title");
  });
});
