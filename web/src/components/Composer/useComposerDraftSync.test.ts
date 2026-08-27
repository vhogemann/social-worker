import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { useComposerDraftSync } from "./useComposerDraftSync";
import { useDraftStore } from "../../store/draftStore";
import { useEditorStore } from "../../store/editorStore";

const mockNavigate = vi.fn();
let mockPathname = "/composer";
let mockParams: Record<string, string | undefined> = {};

vi.mock("react-router-dom", () => ({
  useNavigate: () => mockNavigate,
  useLocation: () => ({ pathname: mockPathname }),
  useParams: () => mockParams,
}));

describe("useComposerDraftSync", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockPathname = "/composer";
    mockParams = {};

    useDraftStore.setState({
      drafts: [
        {
          id: "draft-1",
          title: "Test Draft",
          content: "Hello draft content",
          status: "Draft",
          targetPlatform: null,
          canonicalDraftId: null,
          threads: [],
          createdAt: "2026-08-22T10:00:00Z",
          updatedAt: "2026-08-22T10:00:00Z",
        },
      ],
      activeDraftId: "draft-1",
      loadDrafts: vi.fn().mockResolvedValue(undefined),
      switchDraft: vi.fn().mockResolvedValue({ id: "draft-1", content: "Hello draft content" }),
      createDraft: vi.fn().mockResolvedValue({ id: "draft-new", content: "" }),
    });

    useEditorStore.setState({
      doc: "",
      applyExternal: vi.fn(),
    });
  });

  it("loads drafts on mount and sets isReady to true", async () => {
    const { result } = renderHook(() => useComposerDraftSync());

    await waitFor(() => {
      expect(result.current.isReady).toBe(true);
    });

    expect(useDraftStore.getState().loadDrafts).toHaveBeenCalled();
  });

  it("redirects /composer to active draft ID", async () => {
    mockPathname = "/composer";
    const { result } = renderHook(() => useComposerDraftSync());

    await waitFor(() => {
      expect(result.current.isReady).toBe(true);
    });

    expect(mockNavigate).toHaveBeenCalledWith("/draft/draft-1", { replace: true });
  });

  it("synchronizes active draft content with editorStore when draftId matches", async () => {
    mockPathname = "/draft/draft-1";
    mockParams = { draftId: "draft-1" };
    const { result } = renderHook(() => useComposerDraftSync());

    await waitFor(() => {
      expect(result.current.isReady).toBe(true);
    });

    expect(useEditorStore.getState().applyExternal).toHaveBeenCalledWith("Hello draft content");
  });

  it("creates a draft scoped to workflow when visiting /workflows/:workflowId/draft", async () => {
    mockPathname = "/workflows/wf-1/draft";
    mockParams = { workflowId: "wf-1" };
    const { result } = renderHook(() => useComposerDraftSync());

    await waitFor(() => {
      expect(result.current.isReady).toBe(true);
    });

    expect(useDraftStore.getState().createDraft).toHaveBeenCalledWith(
      undefined,
      undefined,
      undefined,
      undefined,
      "wf-1"
    );
    expect(mockNavigate).toHaveBeenCalledWith("/workflows/wf-1/draft/draft-new", { replace: true });
  });
});
