import React from "react";
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { Badge } from "./Badge";

describe("Badge", () => {
  it("renders with text and appropriate variant styling", () => {
    render(<Badge variant="success">Succeeded</Badge>);
    const badge = screen.getByText("Succeeded");
    expect(badge).toBeInTheDocument();
    expect(badge.className).toContain("text-green-400");
  });
});
