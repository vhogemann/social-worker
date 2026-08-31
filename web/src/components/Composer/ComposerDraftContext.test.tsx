import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, act, waitFor } from "@testing-library/react";
import React from "react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { ComposerDraftProvider, useComposerDraft } from "./ComposerDraftContext";
import * as draftsApi from "../../api/drafts";

vi.mock("../../api/drafts", () => ({
  fetchDraft: vi.fn(),
  patchDraft: vi.fn(),
  patchPlatformThread: vi.fn(),
  publishPlatformThread: vi.fn(),
  fetchSources: vi.fn(),
}));

const mockDraft = {
  id: "draft-1",
  title: "Canonical Draft",
  content: "Initial content",
  status: "Draft",
  targetPlatform: null,
  canonicalDraftId: null,
  threads: [{ id: "thread-1", platform: "Bluesky", stage: "Draft" }],
  variants: [
    {
      id: "var-1",
      title: "Bluesky Variant",
      content: "Variant content",
      status: "Draft",
      targetPlatform: "Bluesky",
      canonicalDraftId: "draft-1",
      threads: [{ id: "thread-var-1", platform: "Bluesky", stage: "Draft" }],
    },
  ],
  sources: [],
  mediaAssets: [],
  createdAt: "2026-01-01",
  updatedAt: "2026-01-01",
};

const mockVariantDraft = mockDraft.variants[0];

const wrapper = ({
  initialRoute = "/draft/draft-1",
  path = "/draft/:draftId",
}: {
  initialRoute?: string;
  path?: string;
}) => {
  return ({ children }: { children: React.ReactNode }) => (
    <MemoryRouter initialEntries={[initialRoute]}>
      <Routes>
        <Route
          path={path}
          element={<ComposerDraftProvider>{children}</ComposerDraftProvider>}
        />
      </Routes>
    </MemoryRouter>
  );
};

describe("ComposerDraftContext", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(draftsApi.fetchDraft).mockImplementation(async (id: string) => {
      if (id === "var-1") return mockVariantDraft as any;
      return mockDraft as any;
    });
    vi.mocked(draftsApi.fetchSources).mockResolvedValue([]);
    vi.mocked(draftsApi.patchDraft).mockResolvedValue(mockDraft as any);
    vi.mocked(draftsApi.patchPlatformThread).mockResolvedValue({ id: "thread-1", stage: "Ready" } as any);
    vi.mocked(draftsApi.publishPlatformThread).mockResolvedValue({ success: true } as any);
  });

  it("loads activeDraft and canonicalDraft for canonical route", async () => {
    const { result } = renderHook(() => useComposerDraft(), {
      wrapper: wrapper({ initialRoute: "/draft/draft-1", path: "/draft/:draftId" }),
    });

    await waitFor(() => {
      expect(result.current.isLoading).toBe(false);
    });

    expect(result.current.activeDraftId).toBe("draft-1");
    expect(result.current.canonicalDraftId).toBe("draft-1");
    expect(result.current.activeDraft?.title).toBe("Canonical Draft");
    expect(result.current.doc).toBe("Initial content");
  });

  it("loads variant draft as activeDraft and fetches canonicalDraft on variant route", async () => {
    const { result } = renderHook(() => useComposerDraft(), {
      wrapper: wrapper({
        initialRoute: "/workflows/wf-1/draft/draft-1/variant/var-1",
        path: "/workflows/:workflowId/draft/:draftId/variant/:variantId",
      }),
    });

    await waitFor(() => {
      expect(result.current.isLoading).toBe(false);
    });

    expect(result.current.activeDraftId).toBe("var-1");
    expect(result.current.canonicalDraftId).toBe("draft-1");
    expect(result.current.workflowId).toBe("wf-1");
    expect(result.current.activeDraft?.id).toBe("var-1");
    expect(result.current.doc).toBe("Variant content");
  });

  it("adds and clears activity cards", async () => {
    const { result } = renderHook(() => useComposerDraft(), {
      wrapper: wrapper({ initialRoute: "/draft/draft-1", path: "/draft/:draftId" }),
    });

    await waitFor(() => {
      expect(result.current.isLoading).toBe(false);
    });

    act(() => {
      result.current.addActivityCard({
        title: "Test Card",
        message: "Test message",
        kind: "info",
      });
    });

    expect(result.current.activityCards).toHaveLength(1);
    expect(result.current.activityCards[0].title).toBe("Test Card");

    act(() => {
      result.current.clearActivityCards();
    });

    expect(result.current.activityCards).toHaveLength(0);
  });

  it("updates panel mode and document", async () => {
    const { result } = renderHook(() => useComposerDraft(), {
      wrapper: wrapper({ initialRoute: "/draft/draft-1", path: "/draft/:draftId" }),
    });

    await waitFor(() => {
      expect(result.current.isLoading).toBe(false);
    });

    act(() => {
      result.current.setPanelMode("preview");
      result.current.setDoc("New custom content");
    });

    expect(result.current.panelMode).toBe("preview");
    expect(result.current.doc).toBe("New custom content");
  });
});
