import React from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { Input } from "./Input";

describe("Input", () => {
  it("renders with label and reflects change events", () => {
    const handleChange = vi.fn();
    render(<Input label="Feed Title" value="Test" onChange={handleChange} />);

    expect(screen.getByLabelText(/feed title/i)).toBeInTheDocument();
    const input = screen.getByDisplayValue("Test");
    fireEvent.change(input, { target: { value: "New Title" } });
    expect(handleChange).toHaveBeenCalled();
  });

  it("displays error and hint correctly", () => {
    const { rerender } = render(<Input label="Feed URL" hint="Enter valid RSS" />);
    expect(screen.getByText("Enter valid RSS")).toBeInTheDocument();

    rerender(<Input label="Feed URL" hint="Enter valid RSS" error="Invalid URL format" />);
    expect(screen.getByText("Invalid URL format")).toBeInTheDocument();
    expect(screen.queryByText("Enter valid RSS")).not.toBeInTheDocument();
  });
});
