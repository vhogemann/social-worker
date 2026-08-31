import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import React from "react";
import { MarkdownEditor } from "./MarkdownEditor";
import { useDraftStore } from "../../store/draftStore";

const setDoc = vi.fn();
let mockActiveDraft = {
  id: "draft-1",
  title: "Draft",
  status: "Draft",
  content: "Initial draft",
  targetPlatform: "Bluesky",
  canonicalDraftId: null,
  threads: [],
  mediaAssets: [],
  createdAt: "2026-07-10T00:00:00Z",
  updatedAt: "2026-07-10T00:00:00Z",
};

vi.mock("../Composer/ComposerDraftContext", () => ({
  useComposerDraft: () => ({
    doc: "Initial draft",
    version: 1,
    setDoc,
    activeDraft: useDraftStore.getState().drafts[0] ?? null,
    activeDraftId: useDraftStore.getState().activeDraftId,
  }),
}));

vi.mock("@uiw/react-codemirror", () => ({
  default: ({ value, editable, onChange }: any) => {
    return (
      <textarea
        data-testid="codemirror"
        aria-label="markdown editor"
        disabled={!editable}
        value={value}
        onChange={(event) => onChange?.(event.target.value)}
      />
    );
  },
}));

describe("MarkdownEditor", () => {
  const uploadFileSource = vi.fn().mockResolvedValue({ markdownLink: "[file](file://source-1)" });
  const uploadMediaAsset = vi.fn().mockResolvedValue({ markdownTag: "![image](media://asset-1)" });

  beforeEach(() => {
    vi.clearAllMocks();
    mockActiveDraft = {
      id: "draft-1",
      title: "Draft",
      status: "Draft",
      content: "Initial draft",
      targetPlatform: "Bluesky",
      canonicalDraftId: null,
      threads: [],
      mediaAssets: [],
      createdAt: "2026-07-10T00:00:00Z",
      updatedAt: "2026-07-10T00:00:00Z",
    };

    useDraftStore.setState({
      activeDraftId: "draft-1",
      drafts: [mockActiveDraft as any],
      uploadFileSource,
      uploadMediaAsset,
    });
  });

  it("updates the editor content on change", () => {
    render(<MarkdownEditor />);

    fireEvent.change(screen.getByTestId("codemirror"), { target: { value: "Updated draft" } });

    expect(setDoc).toHaveBeenCalledWith("Updated draft");
  });

  it("uploads dropped document files into the draft", async () => {
    const { container } = render(<MarkdownEditor />);
    const file = new File(["hello"], "notes.txt", { type: "text/plain" });

    fireEvent.drop(container.firstChild as Element, {
      preventDefault: vi.fn(),
      dataTransfer: { files: [file] },
    });

    await waitFor(() => {
      expect(uploadFileSource).toHaveBeenCalledWith("draft-1", file);
      expect(setDoc).toHaveBeenCalledWith("Initial draft\n\n[file](file://source-1)");
    });
  });

  it("uploads pasted images into the draft", async () => {
    const { container } = render(<MarkdownEditor />);
    const file = new File(["image"], "image.png", { type: "image/png" });
    const item = {
      type: "image/png",
      getAsFile: () => file,
    };

    fireEvent.paste(container.firstChild as Element, {
      preventDefault: vi.fn(),
      clipboardData: { items: [item] },
    });

    await waitFor(() => {
      expect(uploadMediaAsset).toHaveBeenCalledWith("draft-1", file);
      expect(setDoc).toHaveBeenCalledWith("Initial draft\n\n![image](media://asset-1)");
    });
  });

  it("shows the locked overlay when the active draft is sourcing", () => {
    useDraftStore.setState({
      drafts: [
        {
          id: "draft-1",
          title: "Draft",
          status: "Sourcing",
          content: "Initial draft",
          targetPlatform: "Bluesky",
          canonicalDraftId: null,
          threads: [],
          mediaAssets: [],
          createdAt: "2026-07-10T00:00:00Z",
          updatedAt: "2026-07-10T00:00:00Z",
        },
      ],
    });

    render(<MarkdownEditor />);

    expect(screen.getByText("Fetching Sources...")).toBeInTheDocument();
  });
});