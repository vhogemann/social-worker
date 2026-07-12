import { describe, it, expect, vi, beforeEach, type Mock } from "vitest";
import { listBrandVoices, createBrandVoice, updateBrandVoice, deleteBrandVoice } from "./brandVoices";

vi.mock("./client", () => ({
  apiFetch: vi.fn(),
}));

import { apiFetch } from "./client";
const mockApiFetch = apiFetch as Mock;

const makeVoice = (id = "bv1") => ({
  id,
  name: "Tech Writer",
  body: "Write concise technical content.",
  isDefault: true,
  createdAt: "2026-01-01",
  updatedAt: "2026-01-01",
});

describe("brandVoices API", () => {
  beforeEach(() => {
    mockApiFetch.mockReset();
  });

  describe("listBrandVoices", () => {
    it("returns brand voice array", async () => {
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify([makeVoice()]), { status: 200 })
      );
      const result = await listBrandVoices();
      expect(result).toHaveLength(1);
      expect(result[0].name).toBe("Tech Writer");
    });

    it("throws on failure", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response("{}", { status: 500 }));
      await expect(listBrandVoices()).rejects.toThrow("Failed to fetch brand voices");
    });
  });

  describe("createBrandVoice", () => {
    it("sends POST and returns created voice", async () => {
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify(makeVoice("bv2")), { status: 200 })
      );
      const result = await createBrandVoice({ name: "Tech Writer", body: "...", isDefault: false });
      expect(result.id).toBe("bv2");
      expect(mockApiFetch).toHaveBeenCalledWith(
        "/api/brand-prompts",
        expect.objectContaining({ method: "POST" })
      );
    });

    it("throws on failure", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response("{}", { status: 400 }));
      await expect(createBrandVoice({ name: "x", body: "x", isDefault: false })).rejects.toThrow(
        "Failed to create brand voice"
      );
    });
  });

  describe("updateBrandVoice", () => {
    it("sends PUT and returns updated voice", async () => {
      const updated = { ...makeVoice(), name: "Renamed" };
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify(updated), { status: 200 })
      );
      const result = await updateBrandVoice("bv1", { name: "Renamed", body: "...", isDefault: true });
      expect(result.name).toBe("Renamed");
      expect(mockApiFetch).toHaveBeenCalledWith(
        "/api/brand-prompts/bv1",
        expect.objectContaining({ method: "PUT" })
      );
    });

    it("throws on failure", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response("{}", { status: 404 }));
      await expect(updateBrandVoice("bad", { name: "x", body: "x", isDefault: false })).rejects.toThrow(
        "Failed to update brand voice"
      );
    });
  });

  describe("deleteBrandVoice", () => {
    it("sends DELETE and resolves on success", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response(null, { status: 204 }));
      await expect(deleteBrandVoice("bv1")).resolves.toBeUndefined();
      expect(mockApiFetch).toHaveBeenCalledWith(
        "/api/brand-prompts/bv1",
        expect.objectContaining({ method: "DELETE" })
      );
    });

    it("throws on failure", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response("{}", { status: 404 }));
      await expect(deleteBrandVoice("bad")).rejects.toThrow("Failed to delete brand voice");
    });
  });
});
