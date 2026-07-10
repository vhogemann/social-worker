import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import React from "react";
import { SourcesPanel } from "./SourcesPanel";
import { useDraftStore } from "../../store/draftStore";
import { useEditorStore } from "../../store/editorStore";
import * as draftsApi from "../../api/drafts";

vi.mock("../../api/drafts", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../api/drafts")>();
  return {
    ...actual,
    fetchSourceDetail: vi.fn(),
    deleteSource: vi.fn(),
    fetchSources: vi.fn().mockResolvedValue([])
  };
});

describe("SourcesPanel", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    
    useDraftStore.setState({
      activeDraftId: "draft-1",
      drafts: [
        {
          id: "draft-1",
          title: "Test Draft",
          content: "Draft content",
          status: "Draft",
          createdAt: "2026-07-10",
          updatedAt: "2026-07-10",
          chatHistory: "[]",
          chatSummary: "",
          lastSummarizedMessageCount: 0,
          mediaAssets: [],
          threads: []
        }
      ],
      sources: [
        {
          id: "source-1",
          draftId: "draft-1",
          kind: "Url",
          reference: "https://example.com",
          title: "Example Website",
          addedAt: "2026-07-10"
        }
      ]
    });

    useEditorStore.setState({
      doc: "Draft content"
    });
  });

  it("renders the sources list correctly", () => {
    render(<SourcesPanel />);
    const expandBtn = screen.getByText(/Sources & Images/);
    fireEvent.click(expandBtn);
    expect(screen.getByText("Example Website")).toBeInTheDocument();
  });

  it("opens the preview modal and fetches source details when clicking the preview button", async () => {
    const mockDetail = {
      id: "source-1",
      draftId: "draft-1",
      kind: "Url",
      reference: "https://example.com",
      title: "Example Website",
      content: "This is the fetched site content",
      addedAt: "2026-07-10"
    };

    vi.mocked(draftsApi.fetchSourceDetail).mockResolvedValueOnce(mockDetail);

    render(<SourcesPanel />);

    const expandBtn = screen.getByText(/Sources & Images/);
    fireEvent.click(expandBtn);

    const previewBtn = screen.getByTitle("Preview source content");
    fireEvent.click(previewBtn);

    expect(screen.getByText("Fetching source content...")).toBeInTheDocument();

    await waitFor(() => {
      expect(draftsApi.fetchSourceDetail).toHaveBeenCalledWith("draft-1", "source-1");
    });

    await waitFor(() => {
      expect(screen.getByText("This is the fetched site content")).toBeInTheDocument();
    });
  });
});
