import React from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { TagInput } from "./TagInput";

describe("TagInput", () => {
  it("renders existing tags as chips and allows removing them", () => {
    const handleChange = vi.fn();
    render(
      <TagInput
        tags={["default", "personal"]}
        onChange={handleChange}
        label="Tags"
      />
    );

    expect(screen.getByTestId("tag-chip-default")).toBeInTheDocument();
    expect(screen.getByTestId("tag-chip-personal")).toBeInTheDocument();

    const removePersonalBtn = screen.getByTestId("remove-tag-personal");
    fireEvent.click(removePersonalBtn);
    expect(handleChange).toHaveBeenCalledWith(["default"]);
  });

  it("adds new tags when pressing enter or clicking add", () => {
    const handleChange = vi.fn();
    render(
      <TagInput
        tags={["bot"]}
        onChange={handleChange}
        label="Tags"
      />
    );

    const input = screen.getByTestId("tag-text-input");
    fireEvent.change(input, { target: { value: "Science" } });
    fireEvent.keyDown(input, { key: "Enter" });

    expect(handleChange).toHaveBeenCalledWith(["bot", "science"]);
  });

  it("toggles default tag with quick toggle button", () => {
    const handleChange = vi.fn();
    const { rerender } = render(
      <TagInput
        tags={["bot"]}
        onChange={handleChange}
        label="Tags"
      />
    );

    const toggleBtn = screen.getByTestId("toggle-default-tag-btn");
    expect(toggleBtn).toHaveTextContent("+ Set as Default");
    fireEvent.click(toggleBtn);
    expect(handleChange).toHaveBeenCalledWith(["default", "bot"]);

    // If default is already set, clicking toggles it off
    rerender(
      <TagInput
        tags={["default", "bot"]}
        onChange={handleChange}
        label="Tags"
      />
    );
    expect(screen.getByTestId("toggle-default-tag-btn")).toHaveTextContent("Default Set");
    fireEvent.click(screen.getByTestId("toggle-default-tag-btn"));
    expect(handleChange).toHaveBeenCalledWith(["bot"]);
  });
});
