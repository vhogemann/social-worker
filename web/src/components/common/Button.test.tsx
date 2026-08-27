import React from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { Button } from "./Button";
import { faPlus } from "@fortawesome/free-solid-svg-icons";

describe("Button", () => {
  it("renders text and responds to click", () => {
    const handleClick = vi.fn();
    render(<Button onClick={handleClick}>Click Me</Button>);

    const button = screen.getByRole("button", { name: /click me/i });
    expect(button).toBeInTheDocument();
    fireEvent.click(button);
    expect(handleClick).toHaveBeenCalledTimes(1);
  });

  it("renders with icon and handles loading state", () => {
    const { rerender } = render(
      <Button icon={faPlus} loading={false}>
        Add Item
      </Button>
    );

    const button = screen.getByRole("button", { name: /add item/i });
    expect(button).not.toBeDisabled();

    rerender(
      <Button icon={faPlus} loading={true}>
        Add Item
      </Button>
    );

    expect(button).toBeDisabled();
  });
});
