import React from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { DraftListFooter } from "./DraftListFooter";

describe("DraftListFooter", () => {
  it("toggles archive view when clicked", () => {
    const onToggle = vi.fn();
    render(
      <DraftListFooter
        showArchived={false}
        onToggleShowArchived={onToggle}
      />
    );

    const btn = screen.getByRole("button", { name: /show archived/i });
    expect(btn).toBeInTheDocument();
    fireEvent.click(btn);
    expect(onToggle).toHaveBeenCalledTimes(1);
  });
});
