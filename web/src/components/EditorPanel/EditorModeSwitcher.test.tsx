import React from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { EditorModeSwitcher } from "./EditorModeSwitcher";

describe("EditorModeSwitcher", () => {
  it("renders edit and preview modes and triggers callback on change", () => {
    const onChange = vi.fn();
    render(<EditorModeSwitcher mode="edit" onChangeMode={onChange} />);

    expect(screen.getByRole("button", { name: "edit" })).toBeInTheDocument();
    const previewBtn = screen.getByRole("button", { name: "preview" });
    expect(previewBtn).toBeInTheDocument();

    fireEvent.click(previewBtn);
    expect(onChange).toHaveBeenCalledWith("preview");
  });
});
