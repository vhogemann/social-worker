import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { SourcesPanel } from "../components/EditorPanel/SourcesPanel";
import { useDraftStore } from "../store/draftStore";
import { useEditorStore } from "../store/editorStore";
import "@testing-library/jest-dom";

globalThis.fetch = vi.fn().mockImplementation(() =>
  Promise.resolve({
    ok: true,
    status: 200,
    json: async () => [],
  })
);

describe("SourcesPanel component", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useDraftStore.setState({
      activeDraftId: "d-1",
      drafts: [
        {
          id: "d-1",
          title: "My Draft",
          status: "Editing",
          content: "Hello check out file://source-1 and https://example.com",
          threads: [],
          createdAt: "",
          updatedAt: "",
        },
      ],
      sources: [
        {
          id: "source-1",
          draftId: "d-1",
          kind: "File",
          reference: "document.pdf",
          title: "Document PDF Title",
          addedAt: "",
        },
        {
          id: "source-2",
          draftId: "d-1",
          kind: "Url",
          reference: "https://example.com",
          title: "Example Site",
          addedAt: "",
        },
      ],
    });

    useEditorStore.setState({
      doc: "Hello check out [Doc](file://source-1) and https://example.com",
      version: 0,
    });
  });

  it("renders header with source count", () => {
    render(<SourcesPanel />);
    expect(screen.getByText("Sources (2)")).toBeInTheDocument();
  });

  it("displays sources when expanded", () => {
    render(<SourcesPanel />);
    
    // Collapsed initially
    expect(screen.queryByText("Document PDF Title")).not.toBeInTheDocument();

    const expandBtn = screen.getByText("Sources (2)");
    fireEvent.click(expandBtn);

    expect(screen.getByText("Document PDF Title")).toBeInTheDocument();
    expect(screen.getByText("Example Site")).toBeInTheDocument();
  });

  it("removes markdown link reference when trash button is clicked", () => {
    render(<SourcesPanel />);
    
    // Expand drawer
    fireEvent.click(screen.getByText("Sources (2)"));

    const trashButtons = screen.getAllByRole("button", { name: /remove/i });
    expect(trashButtons).toHaveLength(2);

    // Delete the file source
    fireEvent.click(trashButtons[0]);

    // Check that the editor store state has been updated to strip file reference
    const currentDoc = useEditorStore.getState().doc;
    expect(currentDoc).not.toContain("[Doc](file://source-1)");
    expect(currentDoc).toContain("https://example.com");
  });
});
