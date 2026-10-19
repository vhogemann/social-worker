import { describe, it, expect, vi, beforeEach, type Mock } from "vitest";
import {
  fetchWorkflows,
  fetchWorkflow,
  fetchWorkflowDrafts,
  fetchWorkflowQueue,
  createWorkflow,
  updateWorkflow,
  deleteWorkflow,
  triggerWorkflow,
  exportWorkflows,
  importWorkflows,
  type AutomationWorkflowDto,
} from "./workflows";

vi.mock("./client", () => ({
  apiFetch: vi.fn(),
}));

import { apiFetch } from "./client";
const mockApiFetch = apiFetch as Mock;

const makeWorkflow = (id = "w1"): AutomationWorkflowDto => ({
  id,
  title: "Tech News Workflow",
  instructionPrompt: "Summarize as a thread.",
  sources: [{ feedSubscriptionId: "f1", title: "Feed 1", feedUrl: "https://example.com/feed.xml" }],
  destinations: [{ id: "d1", platform: "Bluesky", autoPublish: true }],
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: "2026-01-01T00:00:00Z",
});

describe("workflows API", () => {
  beforeEach(() => {
    mockApiFetch.mockReset();
  });

  describe("fetchWorkflows", () => {
    it("returns workflow array on success", async () => {
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify([makeWorkflow()]), { status: 200 })
      );
      const result = await fetchWorkflows();
      expect(result).toHaveLength(1);
      expect(result[0].title).toBe("Tech News Workflow");
      expect(result[0].sources).toHaveLength(1);
      expect(result[0].destinations).toHaveLength(1);
    });

    it("throws on failure", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response("{}", { status: 500 }));
      await expect(fetchWorkflows()).rejects.toThrow("fetchWorkflows failed: 500");
    });
  });

  describe("createWorkflow", () => {
    it("sends POST and returns created workflow", async () => {
      const created = makeWorkflow("w2");
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify(created), { status: 201 })
      );

      const result = await createWorkflow({
        title: "Tech News Workflow",
        instructionPrompt: "Summarize as a thread.",
        feedSubscriptionIds: ["f1"],
        destinations: [{ platform: "Bluesky", autoPublish: true }],
      });

      expect(result.id).toBe("w2");
      expect(mockApiFetch).toHaveBeenCalledWith(
        "/api/workflows",
        expect.objectContaining({ method: "POST" })
      );
    });
  });

  describe("updateWorkflow", () => {
    it("sends PUT and returns updated workflow", async () => {
      const updated = { ...makeWorkflow("w1"), title: "Updated Title" };
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify(updated), { status: 200 })
      );

      const result = await updateWorkflow("w1", {
        title: "Updated Title",
      });

      expect(result.title).toBe("Updated Title");
      expect(mockApiFetch).toHaveBeenCalledWith(
        "/api/workflows/w1",
        expect.objectContaining({ method: "PUT" })
      );
    });
  });

  describe("deleteWorkflow", () => {
    it("sends DELETE and resolves on 204", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response(null, { status: 204 }));
      await expect(deleteWorkflow("w1")).resolves.toBeUndefined();
      expect(mockApiFetch).toHaveBeenCalledWith(
        "/api/workflows/w1",
        expect.objectContaining({ method: "DELETE" })
      );
    });
  });

  describe("fetchWorkflow", () => {
    it("returns a single workflow on success", async () => {
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify(makeWorkflow("w1")), { status: 200 })
      );
      const result = await fetchWorkflow("w1");
      expect(result.id).toBe("w1");
      expect(mockApiFetch).toHaveBeenCalledWith("/api/workflows/w1");
    });

    it("throws on failure", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response("{}", { status: 404 }));
      await expect(fetchWorkflow("w999")).rejects.toThrow("fetchWorkflow failed: 404");
    });
  });

  describe("fetchWorkflowDrafts", () => {
    it("returns draft summaries for a workflow", async () => {
      const mockDrafts = [
        {
          id: "d1",
          title: "Draft Title",
          status: "Formatting",
          targetPlatform: null,
          threads: [{ platform: "Bluesky", stage: "Sent", accountHandle: "test.bsky.social" }],
          createdAt: "2026-01-01T00:00:00Z",
          updatedAt: "2026-01-01T00:00:00Z"
        }
      ];
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify(mockDrafts), { status: 200 })
      );

      const result = await fetchWorkflowDrafts("w1");
      expect(result).toHaveLength(1);
      expect(result[0].title).toBe("Draft Title");
      expect(mockApiFetch).toHaveBeenCalledWith("/api/workflows/w1/drafts");
    });
  });

  describe("fetchWorkflowQueue", () => {
    it("calls queue endpoint with workflowId query param", async () => {
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify([]), { status: 200 })
      );

      const result = await fetchWorkflowQueue("w1");
      expect(result).toEqual([]);
      expect(mockApiFetch).toHaveBeenCalledWith("/api/feeds/queue?workflowId=w1");
    });
  });

  describe("triggerWorkflow", () => {
    it("sends POST to /trigger and resolves on success", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response(JSON.stringify({ success: true }), { status: 200 }));
      await expect(triggerWorkflow("w1")).resolves.toBeUndefined();
      expect(mockApiFetch).toHaveBeenCalledWith(
        "/api/workflows/w1/trigger",
        expect.objectContaining({ method: "POST" })
      );
    });
  });

  describe("exportWorkflows", () => {
    it("exports all workflows when no id is provided", async () => {
      const mockPkg = { version: 1, exportedAt: "2026-10-19T10:00:00Z", workflows: [] };
      mockApiFetch.mockResolvedValueOnce(new Response(JSON.stringify(mockPkg), { status: 200 }));

      const res = await exportWorkflows();
      expect(res).toEqual(mockPkg);
      expect(mockApiFetch).toHaveBeenCalledWith("/api/workflows/export");
    });

    it("exports single workflow when id is provided", async () => {
      const mockPkg = { version: 1, exportedAt: "2026-10-19T10:00:00Z", workflows: [] };
      mockApiFetch.mockResolvedValueOnce(new Response(JSON.stringify(mockPkg), { status: 200 }));

      const res = await exportWorkflows("w1");
      expect(res).toEqual(mockPkg);
      expect(mockApiFetch).toHaveBeenCalledWith("/api/workflows/w1/export");
    });

    it("throws when export returns error", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response("{}", { status: 500 }));
      await expect(exportWorkflows()).rejects.toThrow("exportWorkflows failed: 500");
    });
  });

  describe("importWorkflows", () => {
    it("posts import package and returns import summary", async () => {
      const mockPkg = { version: 1, exportedAt: "2026-10-19T10:00:00Z", workflows: [] };
      const mockResult = {
        importedWorkflowsCount: 1,
        createdFeedsCount: 1,
        reusedFeedsCount: 0,
        importedWorkflowIds: ["w1"]
      };
      mockApiFetch.mockResolvedValueOnce(new Response(JSON.stringify(mockResult), { status: 200 }));

      const res = await importWorkflows(mockPkg);
      expect(res).toEqual(mockResult);
      expect(mockApiFetch).toHaveBeenCalledWith(
        "/api/workflows/import",
        expect.objectContaining({
          method: "POST",
          body: JSON.stringify(mockPkg)
        })
      );
    });

    it("throws with error message on failure", async () => {
      const mockPkg = { version: 1, exportedAt: "2026-10-19T10:00:00Z", workflows: [] };
      mockApiFetch.mockResolvedValueOnce(new Response("Invalid package", { status: 400 }));

      await expect(importWorkflows(mockPkg)).rejects.toThrow("Invalid package");
    });
  });
});
