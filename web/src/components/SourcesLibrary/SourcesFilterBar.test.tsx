import React from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { SourcesFilterBar } from "./SourcesFilterBar";

describe("SourcesFilterBar", () => {
  it("handles input changes and submits query", () => {
    const handleChangeQuery = vi.fn();
    const handleSubmit = vi.fn((e) => e.preventDefault());
    const handleReset = vi.fn();

    render(
      <SourcesFilterBar
        query="technology"
        kind="all"
        addedAfter=""
        addedBefore=""
        onChangeQuery={handleChangeQuery}
        onChangeKind={() => {}}
        onChangeAddedAfter={() => {}}
        onChangeAddedBefore={() => {}}
        onSubmit={handleSubmit}
        onReset={handleReset}
      />
    );

    const input = screen.getByDisplayValue("technology");
    fireEvent.change(input, { target: { value: "artificial intelligence" } });
    expect(handleChangeQuery).toHaveBeenCalledWith("artificial intelligence");

    const searchBtn = screen.getByRole("button", { name: /^search$/i });
    fireEvent.click(searchBtn);
    expect(handleSubmit).toHaveBeenCalled();

    const resetBtn = screen.getByRole("button", { name: /reset/i });
    fireEvent.click(resetBtn);
    expect(handleReset).toHaveBeenCalled();
  });
});
