import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import React from "react";
import { SourcesPanel } from "./SourcesPanel";
import { useDraftStore } from "../../../store/draftStore";
import { useEditorStore } from "../../../store/editorStore";
import * as draftsApi from "../../../api/drafts";
import { MemoryRouter } from "react-router-dom";

vi.mock("../../../api/drafts", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../api/drafts")>();
  return {
    ...actual,
    fetchSourceDetail: vi.fn(),
    fetchSourceStatus: vi.fn(),
    retrySourceTranscription: vi.fn(),
    deleteSource: vi.fn(),
    searchSources: vi.fn(),
    linkSourceToDraft: vi.fn(),
    fetchSources: vi.fn().mockResolvedValue([])
  };
});

describe("SourcesPanel", () => {
  beforeEach(() => {
    vi.clearAllMocks();

    vi.mocked(draftsApi.fetchSourceStatus).mockResolvedValue({
      sourceId: "source-1",
      processingStatus: "Complete",
      summary: "Short summary",
      youtubeVideoId: null,
    });
    
    useDraftStore.setState({
      activeDraftId: "draft-1",
      drafts: [
        {
          id: "draft-1",
          title: "Test Draft",
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
      sources: [
        {
          id: "source-1",
          draftId: "draft-1",
          kind: "Url",
          reference: "https://example.com",
          title: "Example Website",
          summary: "Short summary",
          processingStatus: "Complete",
          youtubeVideoId: null,
          addedAt: "2026-07-10"
        }
      ]
    });

    useEditorStore.setState({
      doc: "Draft content"
    });
  });

  it("renders the sources list correctly", () => {
    render(
      <MemoryRouter>
        <SourcesPanel />
      </MemoryRouter>
    );
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
      summary: "Short summary",
      processingStatus: "Complete",
      youtubeVideoId: null,
      addedAt: "2026-07-10"
    };

    vi.mocked(draftsApi.fetchSourceDetail).mockResolvedValueOnce(mockDetail);

    render(
      <MemoryRouter>
        <SourcesPanel />
      </MemoryRouter>
    );

    const expandBtn = screen.getByText(/Sources & Images/);
    fireEvent.click(expandBtn);

    const previewBtn = screen.getByTitle("Preview source content");
    fireEvent.click(previewBtn);

    await waitFor(() => {
      expect(draftsApi.fetchSourceDetail).toHaveBeenCalledWith("draft-1", "source-1");
    });

    await waitFor(() => {
      expect(screen.getByText("This is the fetched site content")).toBeInTheDocument();
    });
  });

  it("searches the source library and links a result to the active draft", async () => {
    vi.mocked(draftsApi.searchSources).mockResolvedValueOnce({
      items: [
        {
          id: "source-2",
          kind: "Url",
          reference: "https://reusable.example.com",
          title: "Reusable Source",
          summary: "Shared summary",
          processingStatus: "Complete",
          youtubeVideoId: null,
          addedAt: "2026-07-10",
        },
      ],
      total: 1,
      page: 1,
      pageSize: 20,
    });
    vi.mocked(draftsApi.linkSourceToDraft).mockResolvedValueOnce({
      id: "source-2",
      draftId: "draft-1",
      kind: "Url",
      reference: "https://reusable.example.com",
      title: "Reusable Source",
      summary: "Shared summary",
      processingStatus: "Complete",
      youtubeVideoId: null,
      addedAt: "2026-07-10",
    });

    render(
      <MemoryRouter>
        <SourcesPanel />
      </MemoryRouter>
    );

    fireEvent.click(screen.getByText(/Sources & Images/));
    fireEvent.change(screen.getByPlaceholderText(/Find reusable sources across drafts/i), {
      target: { value: "reusable" },
    });
    fireEvent.click(screen.getByText("Search"));

    await waitFor(() => {
      expect(draftsApi.searchSources).toHaveBeenCalledWith("reusable");
    });

    await waitFor(() => {
      expect(screen.getByText("Reusable Source")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByText("Link"));

    await waitFor(() => {
      expect(draftsApi.linkSourceToDraft).toHaveBeenCalledWith("draft-1", "source-2");
    });
  });

  it("retries transcription for a failed YouTube source from the preview modal", async () => {
    useDraftStore.setState({
      sources: [
        {
          id: "source-yt-1",
          draftId: "draft-1",
          kind: "YouTube",
          reference: "https://www.youtube.com/watch?v=abc123xyz09",
          title: "Example Video",
          summary: "Previous failure",
          processingStatus: "Failed",
          youtubeVideoId: "abc123xyz09",
          addedAt: "2026-07-10",
        },
      ],
    });

    vi.mocked(draftsApi.fetchSourceDetail).mockResolvedValueOnce({
      id: "source-yt-1",
      draftId: "draft-1",
      kind: "YouTube",
      reference: "https://www.youtube.com/watch?v=abc123xyz09",
      title: "Example Video",
      content: "",
      summary: "Previous failure",
      processingStatus: "Failed",
      youtubeVideoId: "abc123xyz09",
      addedAt: "2026-07-10",
    });

    vi.mocked(draftsApi.retrySourceTranscription).mockResolvedValueOnce({
      sourceId: "source-yt-1",
      processingStatus: "Pending",
      summary: null,
      youtubeVideoId: "abc123xyz09",
    });

    render(
      <MemoryRouter>
        <SourcesPanel />
      </MemoryRouter>
    );

    fireEvent.click(screen.getByText(/Sources & Images/));
    fireEvent.click(screen.getByTitle("Preview source content"));

    await waitFor(() => {
      expect(screen.getByText("Retry Transcription")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByText("Retry Transcription"));

    await waitFor(() => {
      expect(draftsApi.retrySourceTranscription).toHaveBeenCalledWith("source-yt-1");
    });
  });

  it("shows tabbed YouTube preview and switches to transcript tab", async () => {
    useDraftStore.setState({
      sources: [
        {
          id: "source-yt-2",
          draftId: "draft-1",
          kind: "YouTube",
          reference: "https://www.youtube.com/watch?v=abc123xyz09",
          title: "Tabbed Video",
          summary: null,
          processingStatus: "Complete",
          youtubeVideoId: "abc123xyz09",
          addedAt: "2026-07-10",
        },
      ],
    });

    vi.mocked(draftsApi.fetchSourceDetail).mockResolvedValueOnce({
      id: "source-yt-2",
      draftId: "draft-1",
      kind: "YouTube",
      reference: "https://www.youtube.com/watch?v=abc123xyz09",
      title: "Tabbed Video",
      content: "Transcript content in tab",
      summary: null,
      processingStatus: "Complete",
      youtubeVideoId: "abc123xyz09",
      addedAt: "2026-07-10",
    });

    render(
      <MemoryRouter>
        <SourcesPanel />
      </MemoryRouter>
    );

    fireEvent.click(screen.getByText(/Sources & Images/));
    fireEvent.click(screen.getByTitle("Preview source content"));

    await waitFor(() => {
      expect(screen.getByText("Video")).toBeInTheDocument();
      expect(screen.getByText("Transcript")).toBeInTheDocument();
    });

    expect(screen.queryByText("Transcript content in tab")).not.toBeInTheDocument();

    fireEvent.click(screen.getByText("Transcript"));

    await waitFor(() => {
      expect(screen.getByText("Transcript content in tab")).toBeInTheDocument();
    });
  });
});
