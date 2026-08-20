import { describe, it, expect, vi, beforeEach, type Mock } from "vitest";
import {
  fetchWorkflows,
  createWorkflow,
  updateWorkflow,
  deleteWorkflow,
  triggerWorkflow,
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
});
