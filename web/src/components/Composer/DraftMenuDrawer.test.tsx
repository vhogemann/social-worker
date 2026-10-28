import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { DraftMenuDrawer } from "./DraftMenuDrawer";

describe("DraftMenuDrawer", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("does not render when isOpen is false", () => {
    render(
      <MemoryRouter>
        <DraftMenuDrawer
          isOpen={false}
          onClose={vi.fn()}
          activeView="preview"
          onSelectView={vi.fn()}
        />
      </MemoryRouter>
    );

    expect(screen.queryByTestId("draft-menu-drawer")).not.toBeInTheDocument();
  });

  it("renders views and triggers onSelectView and onClose when a view is clicked", () => {
    const onClose = vi.fn();
    const onSelectView = vi.fn();

    render(
      <MemoryRouter>
        <DraftMenuDrawer
          isOpen={true}
          onClose={onClose}
          activeView="preview"
          onSelectView={onSelectView}
        />
      </MemoryRouter>
    );

    expect(screen.getByTestId("draft-menu-drawer")).toBeInTheDocument();
    expect(screen.getByTestId("menu-view-preview")).toBeInTheDocument();
    expect(screen.getByTestId("menu-view-editor")).toBeInTheDocument();
    expect(screen.getByTestId("menu-view-chat")).toBeInTheDocument();

    // Click editor view
    fireEvent.click(screen.getByTestId("menu-view-editor"));
    expect(onSelectView).toHaveBeenCalledWith("editor");
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it("closes when close button is clicked or Escape key is pressed", () => {
    const onClose = vi.fn();

    render(
      <MemoryRouter>
        <DraftMenuDrawer
          isOpen={true}
          onClose={onClose}
          activeView="preview"
          onSelectView={vi.fn()}
        />
      </MemoryRouter>
    );

    fireEvent.click(screen.getByTestId("draft-menu-close-btn"));
    expect(onClose).toHaveBeenCalledTimes(1);

    fireEvent.keyDown(window, { key: "Escape" });
    expect(onClose).toHaveBeenCalledTimes(2);
  });

  it("handles copy draft link", async () => {
    const writeTextMock = vi.fn().mockResolvedValue(undefined);
    Object.assign(navigator, {
      clipboard: {
        writeText: writeTextMock,
      },
    });

    render(
      <MemoryRouter>
        <DraftMenuDrawer
          isOpen={true}
          onClose={vi.fn()}
          activeView="preview"
          onSelectView={vi.fn()}
        />
      </MemoryRouter>
    );

    const copyBtn = screen.getByTestId("menu-copy-draft-link");
    expect(copyBtn).toHaveTextContent("Copy Draft Link");

    fireEvent.click(copyBtn);
    expect(writeTextMock).toHaveBeenCalledWith(window.location.href);
  });

  it("triggers adapt modal and closes drawer", () => {
    const onClose = vi.fn();
    const onOpenAdaptModal = vi.fn();

    render(
      <MemoryRouter>
        <DraftMenuDrawer
          isOpen={true}
          onClose={onClose}
          activeView="preview"
          onSelectView={vi.fn()}
          onOpenAdaptModal={onOpenAdaptModal}
        />
      </MemoryRouter>
    );

    const adaptBtn = screen.getByTestId("menu-adapt-platforms");
    fireEvent.click(adaptBtn);

    expect(onOpenAdaptModal).toHaveBeenCalledTimes(1);
    expect(onClose).toHaveBeenCalledTimes(1);
  });
});
