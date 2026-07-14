import { describe, it, expect, vi, beforeEach, type Mock } from "vitest";
import {
  fetchDrafts,
  createDraft,
  fetchDraft,
  patchDraft,
  fetchPlatformThreads,
  createPlatformThread,
  patchPlatformThread,
  publishPlatformThread,
  fetchSources,
  fetchSourceDetail,
  fetchSourceStatus,
  retrySourceTranscription,
  deleteSource,
  importSourceFromUrl,
  linkSourceToDraft,
  searchSources,
  uploadFile,
  uploadMedia,
  renderCodeImage,
  patchMediaAsset,
  deleteMediaAsset,
  generateVariants,
} from "./drafts";

vi.mock("./client", () => ({
  apiFetch: vi.fn(),
}));

import { apiFetch } from "./client";
const mockApiFetch = apiFetch as Mock;

const makeDraft = (id = "d1") => ({
  id,
  title: "Draft",
  status: "Draft",
  content: null,
  targetPlatform: "Bluesky",
  canonicalDraftId: null,
  threads: [],
  createdAt: "2026-01-01",
  updatedAt: "2026-01-01",
});

const makeThread = (id = "t1") => ({
  id,
  draftId: "d1",
  platform: "Bluesky",
  stage: "Draft",
  content: null,
  posts: [],
});

describe("drafts API", () => {
  beforeEach(() => {
    mockApiFetch.mockReset();
  });

  describe("fetchDrafts", () => {
    it("returns draft array", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response(JSON.stringify([makeDraft()]), { status: 200 }));
      const result = await fetchDrafts();
      expect(result).toHaveLength(1);
      expect(result[0].id).toBe("d1");
    });

    it("throws on failure", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response("{}", { status: 500 }));
      await expect(fetchDrafts()).rejects.toThrow("fetchDrafts failed");
    });
  });

  describe("createDraft", () => {
    it("posts with title and content and returns draft", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response(JSON.stringify(makeDraft()), { status: 200 }));
      const result = await createDraft("My Draft", "Hello", "Bluesky");
      expect(mockApiFetch).toHaveBeenCalledWith("/api/drafts", expect.objectContaining({ method: "POST" }));
      expect(result.id).toBe("d1");
    });

    it("throws on failure", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response("{}", { status: 400 }));
      await expect(createDraft()).rejects.toThrow("createDraft failed");
    });
  });

  describe("fetchDraft", () => {
    it("returns draft by id", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response(JSON.stringify(makeDraft("d2")), { status: 200 }));
      const result = await fetchDraft("d2");
      expect(result.id).toBe("d2");
    });

    it("throws on failure", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response("{}", { status: 404 }));
      await expect(fetchDraft("bad")).rejects.toThrow("fetchDraft failed");
    });
  });

  describe("patchDraft", () => {
    it("sends PATCH and returns updated draft", async () => {
      const updated = { ...makeDraft(), title: "New Title" };
      mockApiFetch.mockResolvedValueOnce(new Response(JSON.stringify(updated), { status: 200 }));
      const result = await patchDraft("d1", { title: "New Title" });
      expect(result.title).toBe("New Title");
      expect(mockApiFetch).toHaveBeenCalledWith("/api/drafts/d1", expect.objectContaining({ method: "PATCH" }));
    });
  });

  describe("fetchPlatformThreads", () => {
    it("returns thread array", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response(JSON.stringify([makeThread()]), { status: 200 }));
      const result = await fetchPlatformThreads("d1");
      expect(result).toHaveLength(1);
    });
  });

  describe("createPlatformThread", () => {
    it("returns created thread", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response(JSON.stringify(makeThread()), { status: 200 }));
      const result = await createPlatformThread("d1", "Bluesky");
      expect(result.platform).toBe("Bluesky");
    });
  });

  describe("patchPlatformThread", () => {
    it("sends PATCH and returns updated thread", async () => {
      const updated = { ...makeThread(), stage: "Ready" };
      mockApiFetch.mockResolvedValueOnce(new Response(JSON.stringify(updated), { status: 200 }));
      const result = await patchPlatformThread("d1", "t1", { stage: "Ready" });
      expect(result.stage).toBe("Ready");
    });
  });

  describe("publishPlatformThread", () => {
    it("returns PublishResult on success", async () => {
      const result = { success: true, posts: [] };
      mockApiFetch.mockResolvedValueOnce(new Response(JSON.stringify(result), { status: 200 }));
      const res = await publishPlatformThread("d1", "t1");
      expect(res.success).toBe(true);
    });

    it("throws with server error on failure", async () => {
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify({ error: "Not ready" }), { status: 400 })
      );
      await expect(publishPlatformThread("d1", "t1")).rejects.toThrow("Not ready");
    });
  });

  describe("fetchSources", () => {
    it("returns source array", async () => {
      const sources = [{ id: "s1", draftId: "d1", kind: "Url", reference: "https://x.com", title: null, addedAt: "2026-01-01" }];
      mockApiFetch.mockResolvedValueOnce(new Response(JSON.stringify(sources), { status: 200 }));
      const result = await fetchSources("d1");
      expect(result[0].id).toBe("s1");
    });
  });

  describe("fetchSourceDetail", () => {
    it("returns source detail", async () => {
      const detail = { id: "s1", draftId: "d1", kind: "Url", reference: "https://x.com", title: null, content: "text", addedAt: "2026-01-01" };
      mockApiFetch.mockResolvedValueOnce(new Response(JSON.stringify(detail), { status: 200 }));
      const result = await fetchSourceDetail("d1", "s1");
      expect(result.content).toBe("text");
    });
  });

  describe("searchSources", () => {
    it("returns source library search results", async () => {
      mockApiFetch.mockResolvedValueOnce(
        new Response(
          JSON.stringify({
            items: [
              {
                id: "s1",
                kind: "Url",
                reference: "https://example.com",
                title: "Example",
                summary: "Summary",
                transcriptStatus: "Complete",
                youtubeVideoId: null,
                addedAt: "2026-01-01",
              },
            ],
            total: 1,
            page: 1,
            pageSize: 20,
          }),
          { status: 200 }
        )
      );

      const result = await searchSources("example");
      expect(result.items[0].id).toBe("s1");
      expect(result.total).toBe(1);
    });
  });

  describe("fetchSourceStatus", () => {
    it("returns source status payload", async () => {
      mockApiFetch.mockResolvedValueOnce(
        new Response(
          JSON.stringify({
            sourceId: "s1",
            transcriptStatus: "Processing",
            summary: "Summary",
            youtubeVideoId: "abc123xyz09",
          }),
          { status: 200 }
        )
      );

      const result = await fetchSourceStatus("s1");
      expect(result.transcriptStatus).toBe("Processing");
    });
  });

  describe("retrySourceTranscription", () => {
    it("returns source status payload after retry enqueue", async () => {
      mockApiFetch.mockResolvedValueOnce(
        new Response(
          JSON.stringify({
            sourceId: "s1",
            transcriptStatus: "Pending",
            summary: null,
            youtubeVideoId: "abc123xyz09",
          }),
          { status: 200 }
        )
      );

      const result = await retrySourceTranscription("s1");
      expect(result.transcriptStatus).toBe("Pending");
    });
  });

  describe("deleteSource", () => {
    it("resolves on success", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response(null, { status: 204 }));
      await expect(deleteSource("d1", "s1")).resolves.toBeUndefined();
    });

    it("throws on failure", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response("{}", { status: 404 }));
      await expect(deleteSource("d1", "s1")).rejects.toThrow("deleteSource failed");
    });
  });

  describe("importSourceFromUrl", () => {
    it("returns imported source payload", async () => {
      mockApiFetch.mockResolvedValueOnce(
        new Response(
          JSON.stringify({
            sourceId: "s2",
            reference: "https://example.com/article",
            title: "Example",
            kind: "Url",
          }),
          { status: 200 }
        )
      );

      const result = await importSourceFromUrl("d1", "https://example.com/article", "Example");
      expect(result.sourceId).toBe("s2");
      expect(result.kind).toBe("Url");
    });

    it("throws with server message on failure", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response("Invalid URL", { status: 400 }));
      await expect(importSourceFromUrl("d1", "not-a-url")).rejects.toThrow("Invalid URL");
    });
  });

  describe("linkSourceToDraft", () => {
    it("returns linked source dto", async () => {
      mockApiFetch.mockResolvedValueOnce(
        new Response(
          JSON.stringify({
            id: "s1",
            draftId: "d1",
            kind: "Url",
            reference: "https://example.com",
            title: "Example",
            summary: "Summary",
            transcriptStatus: "Complete",
            youtubeVideoId: null,
            addedAt: "2026-01-01",
          }),
          { status: 200 }
        )
      );

      const result = await linkSourceToDraft("d1", "s1");
      expect(result.id).toBe("s1");
      expect(result.draftId).toBe("d1");
    });
  });

  describe("uploadFile", () => {
    it("returns sourceId and markdownLink", async () => {
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify({ sourceId: "s1", markdownLink: "[x](url)" }), { status: 200 })
      );
      const file = new File(["content"], "test.txt", { type: "text/plain" });
      const result = await uploadFile("d1", file);
      expect(result.sourceId).toBe("s1");
    });
  });

  describe("uploadMedia", () => {
    it("returns id and markdownTag", async () => {
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify({ id: "m1", markdownTag: "![alt](url)" }), { status: 200 })
      );
      const file = new File(["img"], "img.png", { type: "image/png" });
      const result = await uploadMedia("d1", file);
      expect(result.id).toBe("m1");
    });

    it("throws with server text on failure", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response("File too large", { status: 413 }));
      const file = new File([""], "big.png");
      await expect(uploadMedia("d1", file)).rejects.toThrow("File too large");
    });
  });

  describe("renderCodeImage", () => {
    it("returns id and markdownTag", async () => {
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify({ id: "ci1", markdownTag: "![code](url)" }), { status: 200 })
      );
      const result = await renderCodeImage("d1", 'console.log("hi")', "typescript", "Dark");
      expect(result.id).toBe("ci1");
    });
  });

  describe("patchMediaAsset", () => {
    it("returns updated media asset", async () => {
      const asset = { id: "m1", draftId: "d1", fileName: "img.png", mimeType: "image/png", altText: "new alt", filePath: "/f", sizeBytes: 100, width: 10, height: 10, createdAt: "2026-01-01" };
      mockApiFetch.mockResolvedValueOnce(new Response(JSON.stringify(asset), { status: 200 }));
      const result = await patchMediaAsset("m1", { altText: "new alt" });
      expect(result.altText).toBe("new alt");
    });
  });

  describe("deleteMediaAsset", () => {
    it("returns success", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response(JSON.stringify({ success: true }), { status: 200 }));
      const result = await deleteMediaAsset("m1");
      expect(result.success).toBe(true);
    });
  });

  describe("generateVariants", () => {
    it("returns canonical and variants", async () => {
      const payload = { canonical: makeDraft("c1"), variants: [makeDraft("v1")] };
      mockApiFetch.mockResolvedValueOnce(new Response(JSON.stringify(payload), { status: 200 }));
      const result = await generateVariants("d1", ["Twitter"]);
      expect(result.canonical.id).toBe("c1");
      expect(result.variants).toHaveLength(1);
    });
  });
});
