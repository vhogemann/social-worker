import React from "react";
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { AppHeader } from "./AppHeader";

describe("AppHeader", () => {
  it("renders brand link and navigation items", () => {
    render(
      <MemoryRouter initialEntries={["/"]}>
        <AppHeader />
      </MemoryRouter>
    );

    expect(screen.getByText("social-worker")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Workflows" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Drafts" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Templates" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Settings" })).toBeInTheDocument();
  });

  it("highlights active navigation link", () => {
    render(
      <MemoryRouter initialEntries={["/templates"]}>
        <AppHeader />
      </MemoryRouter>
    );

    const templatesLink = screen.getByRole("link", { name: "Templates" });
    expect(templatesLink.className).toContain("text-accent");
  });
});
