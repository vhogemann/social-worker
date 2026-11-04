import React from "react";
import { describe, it, expect, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { AppLayout } from "./AppLayout";
import { ColorModeProvider } from "../../theme/ColorModeContext";

describe("AppLayout", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it("renders child content and navigation shell", () => {
    render(
      <ColorModeProvider>
        <MemoryRouter initialEntries={["/"]}>
          <AppLayout>
            <div data-testid="test-content">Workspace Content</div>
          </AppLayout>
        </MemoryRouter>
      </ColorModeProvider>
    );

    expect(screen.getByTestId("test-content")).toHaveTextContent("Workspace Content");
    expect(screen.getByText("social-worker")).toBeInTheDocument();
  });

  it("persists collapsed sidebar state in localStorage", () => {
    render(
      <ColorModeProvider>
        <MemoryRouter initialEntries={["/"]}>
          <AppLayout>
            <div>Workspace Content</div>
          </AppLayout>
        </MemoryRouter>
      </ColorModeProvider>
    );

    const collapseBtn = screen.getByLabelText("Collapse sidebar");
    fireEvent.click(collapseBtn);
    expect(localStorage.getItem("social-worker-sidebar-collapsed")).toBe("true");
  });
});
