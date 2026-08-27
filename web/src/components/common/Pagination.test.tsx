import React from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { Pagination } from "./Pagination";

describe("Pagination", () => {
  it("does not render when totalPages <= 1", () => {
    const { container } = render(
      <Pagination currentPage={1} totalPages={1} onPageChange={() => {}} />
    );
    expect(container).toBeEmptyDOMElement();
  });

  it("renders page counts and handles navigation", () => {
    const handlePageChange = vi.fn();
    render(
      <Pagination
        currentPage={2}
        totalPages={5}
        totalItems={50}
        onPageChange={handlePageChange}
      />
    );

    expect(screen.getByText("Page 2 of 5 (50 items)")).toBeInTheDocument();
    const prevBtn = screen.getByRole("button", { name: /prev/i });
    const nextBtn = screen.getByRole("button", { name: /next/i });

    expect(prevBtn).not.toBeDisabled();
    expect(nextBtn).not.toBeDisabled();

    fireEvent.click(prevBtn);
    expect(handlePageChange).toHaveBeenCalledWith(1);

    fireEvent.click(nextBtn);
    expect(handlePageChange).toHaveBeenCalledWith(3);
  });
});
