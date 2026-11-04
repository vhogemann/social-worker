import React from "react";
import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { AppTopBar } from "./AppTopBar";
import { ColorModeProvider } from "../../theme/ColorModeContext";

describe("AppTopBar", () => {
  it("renders mobile menu button and calls onOpenMobileMenu on click", () => {
    let opened = false;
    render(
      <ColorModeProvider>
        <MemoryRouter initialEntries={["/"]}>
          <AppTopBar
            onOpenMobileMenu={() => {
              opened = true;
            }}
          />
        </MemoryRouter>
      </ColorModeProvider>
    );

    const menuBtn = screen.getByLabelText("Open navigation menu");
    expect(menuBtn).toBeInTheDocument();
    fireEvent.click(menuBtn);
    expect(opened).toBe(true);
  });
});
