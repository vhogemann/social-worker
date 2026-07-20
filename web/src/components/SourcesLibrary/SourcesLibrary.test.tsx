import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import React from "react";
import { MemoryRouter } from "react-router-dom";
import { SourcesLibrary } from "./SourcesLibrary";
import { useDraftStore } from "../../store/draftStore";
import * as draftsApi from "../../api/drafts";

vi.mock("../../api/drafts", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../api/drafts")>();
  return {
    ...actual,
    searchSources: vi.fn(),
    fetchSourceById: vi.fn(),
    linkSourceToDraft: vi.fn(),
    retrySourceTranscription: vi.fn(),
    fetchSources: vi.fn().mockResolvedValue([]),
  };
});

describe("SourcesLibrary", () => {
  beforeEach(() => {
    vi.clearAllMocks();

    vi.mocked(draftsApi.searchSources).mockResolvedValue({
      items: [
        {
          id: "source-1",
          kind: "Url",
          reference: "https://example.com/one",
          title: "Climate Article One",
          summary: "This is a climate change article summary.",
          processingStatus: "Pending",
          youtubeVideoId: null,
          addedAt: "2026-07-16T10:00:00Z"
        },
        {
          id: "source-2",
          kind: "YouTube",
          reference: "https://youtube.com/watch?v=abc",
          title: "Climate Change Video",
          summary: "A video on global warming.",
          processingStatus: "Complete",
          youtubeVideoId: "abc",
          addedAt: "2026-07-15T10:00:00Z"
        }
      ],
      total: 2,
      page: 1,
      pageSize: 12
    });

    useDraftStore.setState({
      activeDraftId: "draft-1",
      drafts: [
        {
          id: "draft-1",
          title: "Composer Draft",
          content: "Draft content",
          status: "Draft",
          targetPlatform: "Bluesky",
          canonicalDraftId: null,
          createdAt: "2026-07-10",
          updatedAt: "2026-07-10",
          chatHistory: "[]",
          chatSummary: "",
          lastSummarizedMessageCount: 0,
          mediaAssets: [],
          threads: []
        }
      ],
      sources: []
    });
  });

  it("renders search results, filters, and page controls", async () => {
    render(
      <MemoryRouter>
        <SourcesLibrary />
      </MemoryRouter>
    );

    // Header check
    expect(screen.getByText("Sources Library")).toBeInTheDocument();

    // Results render verification
    await waitFor(() => {
      expect(screen.getByText("Climate Article One")).toBeInTheDocument();
      expect(screen.getByText("Climate Change Video")).toBeInTheDocument();
    });

    // Reset filters checks
    expect(screen.getByPlaceholderText("Search content, titles, or URLs...")).toBeInTheDocument();
  });

  it("triggers search on query submit", async () => {
    render(
      <MemoryRouter>
        <SourcesLibrary />
      </MemoryRouter>
    );

    const input = screen.getByPlaceholderText("Search content, titles, or URLs...");
    fireEvent.change(input, { target: { value: "global warming" } });

    const searchButton = screen.getByRole("button", { name: "Search" });
    fireEvent.click(searchButton);

    await waitFor(() => {
      expect(draftsApi.searchSources).toHaveBeenCalledWith(
        "global warming",
        1,
        12,
        undefined,
        undefined,
        undefined
      );
    });
  });

  it("links source to draft when Link to Draft is clicked", async () => {
    vi.mocked(draftsApi.linkSourceToDraft).mockResolvedValue({
      id: "source-1",
      draftId: "draft-1",
      kind: "Url",
      reference: "https://example.com/one",
      title: "Climate Article One",
      summary: "This is a climate change article summary.",
      processingStatus: "Pending",
      youtubeVideoId: null,
      addedAt: "2026-07-16T10:00:00Z"
    });

    render(
      <MemoryRouter>
        <SourcesLibrary />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText("Climate Article One")).toBeInTheDocument();
    });

    const linkButton = screen.getAllByRole("button", { name: /Link to Draft|Linking.../i })[0];
    fireEvent.click(linkButton);

    await waitFor(() => {
      expect(draftsApi.linkSourceToDraft).toHaveBeenCalledWith("draft-1", "source-1");
    });
  });
});
