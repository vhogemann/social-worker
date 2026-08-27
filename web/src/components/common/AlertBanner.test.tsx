import React from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { AlertBanner } from "./AlertBanner";

describe("AlertBanner", () => {
  it("renders error message and handles dismissal", () => {
    const handleDismiss = vi.fn();
    render(<AlertBanner message="Failed to load" onDismiss={handleDismiss} />);

    expect(screen.getByText("Failed to load")).toBeInTheDocument();
    const dismissBtn = screen.getByRole("button", { name: /dismiss alert/i });
    fireEvent.click(dismissBtn);
    expect(handleDismiss).toHaveBeenCalledTimes(1);
  });
});
