import React from "react";
import { describe, it, expect, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { AppSidebar } from "./AppSidebar";
import { ColorModeProvider } from "../../theme/ColorModeContext";

describe("AppSidebar", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it("renders all navigation items and brand title when expanded", () => {
    render(
      <ColorModeProvider>
        <MemoryRouter initialEntries={["/"]}>
          <AppSidebar
            isCollapsed={false}
            onToggleCollapse={() => {}}
            mobileOpen={false}
            onCloseMobile={() => {}}
          />
        </MemoryRouter>
      </ColorModeProvider>
    );

    expect(screen.getByText("social-worker")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Workflows/i })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Drafts/i })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Templates/i })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Settings/i })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Calendar/i })).toBeInTheDocument();
  });

  it("triggers toggle collapse callback when clicking the collapse button", () => {
    let collapsedToggled = false;
    render(
      <ColorModeProvider>
        <MemoryRouter initialEntries={["/"]}>
          <AppSidebar
            isCollapsed={false}
            onToggleCollapse={() => {
              collapsedToggled = true;
            }}
            mobileOpen={false}
            onCloseMobile={() => {}}
          />
        </MemoryRouter>
      </ColorModeProvider>
    );

    const collapseBtn = screen.getByLabelText("Collapse sidebar");
    fireEvent.click(collapseBtn);
    expect(collapsedToggled).toBe(true);
  });
});
