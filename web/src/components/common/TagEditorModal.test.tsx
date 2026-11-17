import React from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { TagEditorModal } from "./TagEditorModal";

describe("TagEditorModal", () => {
  it("renders when open and calls onSave with updated tags", async () => {
    const handleSave = vi.fn().mockResolvedValue(undefined);
    const handleClose = vi.fn();

    render(
      <TagEditorModal
        isOpen={true}
        onClose={handleClose}
        title="Edit Connection Tags: @victor"
        initialTags={["personal"]}
        onSave={handleSave}
      />
    );

    expect(screen.getByText("Edit Connection Tags: @victor")).toBeInTheDocument();
    expect(screen.getByTestId("tag-chip-personal")).toBeInTheDocument();

    // Toggle default
    const toggleBtn = screen.getByTestId("toggle-default-tag-btn");
    fireEvent.click(toggleBtn);

    // Click submit
    const submitBtn = screen.getByTestId("save-tags-submit-btn");
    fireEvent.click(submitBtn);

    expect(handleSave).toHaveBeenCalledWith(["default", "personal"]);
  });
});
