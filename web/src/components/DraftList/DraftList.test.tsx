import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import React from "react";
import { DraftList } from "./DraftList";

const mockUseDraftListManager = vi.fn();

vi.mock("./useDraftListManager", () => ({
  useDraftListManager: () => mockUseDraftListManager(),
}));

vi.mock("../Settings/SettingsModal", () => ({
  SettingsModal: () => <div data-testid="settings-modal" />,
}));

vi.mock("./CreateDraftModal", () => ({
  CreateDraftModal: ({ isOpen }: { isOpen: boolean }) => (isOpen ? <div data-testid="create-draft-modal" /> : null),
}));

describe("DraftList", () => {
  const handleSelect = vi.fn();
  const archiveDraft = vi.fn();
  const unarchiveDraft = vi.fn();
  const setSettingsOpen = vi.fn();
  const setEditingTitleId = vi.fn();
  const setEditTitleValue = vi.fn();
  const setShowArchived = vi.fn();
  const setCreateModalOpen = vi.fn();
  const handleNew = vi.fn();
  const handleSaveTitle = vi.fn();
  const startEditTitle = vi.fn();
  const handleDelete = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    mockUseDraftListManager.mockReturnValue({
      activeDraftId: "draft-1",
      archiveDraft,
      unarchiveDraft,
      settingsOpen: false,
      setSettingsOpen,
      editingTitleId: null,
      setEditingTitleId,
      editTitleValue: "",
      setEditTitleValue,
      showArchived: false,
      setShowArchived,
      createModalOpen: false,
      setCreateModalOpen,
      handleSelect,
      handleNew,
      handleSaveTitle,
      startEditTitle,
      handleDelete,
      canonicalDrafts: [
        { id: "draft-1", title: "Active Draft", status: "Draft", targetPlatform: "Bluesky" },
        { id: "draft-2", title: "Second Draft", status: "Archived", targetPlatform: null },
      ],
      variantDrafts: [
        { id: "variant-1", title: "Active Variant", canonicalDraftId: "draft-1", status: "Draft", targetPlatform: "Twitter" },
      ],
    });
  });

  it("renders canonical drafts and their variants", () => {
    render(<DraftList />);

    expect(screen.getByText("Active Draft")).toBeInTheDocument();
    expect(screen.getByText("Active Variant")).toBeInTheDocument();
    expect(screen.getByText("variant")).toBeInTheDocument();
  });

  it("calls draft actions from the row controls", () => {
    render(<DraftList />);

    fireEvent.click(screen.getByText("Active Draft"));
    fireEvent.doubleClick(screen.getByText("Active Draft"));
    fireEvent.click(screen.getByTitle("Archive Draft"));
    fireEvent.click(screen.getAllByTitle("Delete Draft")[0]);
    fireEvent.click(screen.getByText("settings"));
    fireEvent.click(screen.getByText("new"));

    expect(handleSelect).toHaveBeenCalledWith("draft-1");
    expect(startEditTitle).toHaveBeenCalledWith("draft-1", "Active Draft");
    expect(archiveDraft).toHaveBeenCalledWith("draft-1");
    expect(handleDelete).toHaveBeenCalledWith("draft-1");
    expect(setSettingsOpen).toHaveBeenCalledWith(true);
    expect(setCreateModalOpen).toHaveBeenCalledWith(true);
  });
});