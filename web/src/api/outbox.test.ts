import { describe, it, expect, vi, beforeEach, type Mock } from "vitest";
import {
  fetchOutboxDrafts,
  approveOutboxDraft,
  dismissOutboxDraft,
  type OutboxDraftDto
} from "./outbox";

vi.mock("./client", () => ({
  apiFetch: vi.fn(),
}));

import { apiFetch } from "./client";
const mockApiFetch = apiFetch as Mock;

const makeDraft = (draftId = "d1"): OutboxDraftDto => ({
  draftId,
  title: "AI Daily Roundup",
  content: "Summary of AI news",
  workflowId: "w1",
  workflowTitle: "Tech News Workflow",
  threads: [
    {
      threadId: "t1",
      platform: "Bluesky",
      stage: "Draft",
      content: "Summary of AI news",
      accountHandle: "tech.bsky.social"
    }
  ],
  createdAt: "2026-09-07T08:00:00Z",
  updatedAt: "2026-09-07T08:00:00Z"
});

describe("outbox API", () => {
  beforeEach(() => {
    mockApiFetch.mockReset();
  });

  describe("fetchOutboxDrafts", () => {
    it("returns drafts array on success", async () => {
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify([makeDraft()]), { status: 200 })
      );
      const result = await fetchOutboxDrafts();
      expect(result).toHaveLength(1);
      expect(result[0].title).toBe("AI Daily Roundup");
      expect(result[0].threads).toHaveLength(1);
      expect(mockApiFetch).toHaveBeenCalledWith("/api/outbox");
    });

    it("throws on failure", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response("{}", { status: 500 }));
      await expect(fetchOutboxDrafts()).rejects.toThrow("fetchOutboxDrafts failed: 500");
    });
  });

  describe("approveOutboxDraft", () => {
    it("sends POST with queue action", async () => {
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify({ message: "Draft queued" }), { status: 200 })
      );
      const res = await approveOutboxDraft("d1", {
        action: "queue",
        scheduleId: "s1"
      });
      expect(res.message).toBe("Draft queued");
      expect(mockApiFetch).toHaveBeenCalledWith("/api/outbox/d1/approve", {
        method: "POST",
        body: JSON.stringify({ action: "queue", scheduleId: "s1" })
      });
    });

    it("sends POST with publish_now action", async () => {
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify({ message: "Published", postsCount: 1 }), { status: 200 })
      );
      const res = await approveOutboxDraft("d1", { action: "publish_now" });
      expect(res.postsCount).toBe(1);
    });
  });

  describe("dismissOutboxDraft", () => {
    it("sends DELETE and completes on success", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response(null, { status: 204 }));
      await dismissOutboxDraft("d1");
      expect(mockApiFetch).toHaveBeenCalledWith("/api/outbox/d1", {
        method: "DELETE"
      });
    });
  });
});
