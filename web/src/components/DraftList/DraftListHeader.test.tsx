import React from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { DraftListHeader } from "./DraftListHeader";

describe("DraftListHeader", () => {
  it("renders drafts title and new draft button when not archived", () => {
    const onOpen = vi.fn();
    render(<DraftListHeader showArchived={false} onOpenCreateModal={onOpen} />);

    expect(screen.getByText("drafts")).toBeInTheDocument();
    const newBtn = screen.getByRole("button", { name: /new/i });
    expect(newBtn).toBeInTheDocument();
    fireEvent.click(newBtn);
    expect(onOpen).toHaveBeenCalledTimes(1);
  });

  it("renders archived title and hides new button when showArchived is true", () => {
    const onOpen = vi.fn();
    render(<DraftListHeader showArchived={true} onOpenCreateModal={onOpen} />);

    expect(screen.getByText("archived")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /new/i })).not.toBeInTheDocument();
  });
});
