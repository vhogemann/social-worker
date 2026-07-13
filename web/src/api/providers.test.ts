import { describe, it, expect, vi, beforeEach, type Mock } from "vitest";
import {
  listProviders,
  listAvailableProviders,
  createProvider,
  updateProvider,
  deleteProvider,
  testProvider,
} from "./providers";

vi.mock("./client", () => ({
  apiFetch: vi.fn(),
}));

import { apiFetch } from "./client";
const mockApiFetch = apiFetch as Mock;

const makeProvider = (id = "p1") => ({
  id,
  name: "OpenRouter",
  providerType: "OpenRouter",
  baseUrl: "https://openrouter.ai/api/v1",
  apiKeySet: true,
  model: "claude-3-5-sonnet",
  contextWindowTokens: 131072,
  isDefault: true,
  isActive: true,
  supportsVision: true,
  supportsTools: true,
});

describe("providers API", () => {
  beforeEach(() => {
    mockApiFetch.mockReset();
  });

  describe("listProviders", () => {
    it("returns provider array", async () => {
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify([makeProvider()]), { status: 200 })
      );
      const result = await listProviders();
      expect(result).toHaveLength(1);
      expect(result[0].id).toBe("p1");
    });

    it("throws on failure", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response("{}", { status: 500 }));
      await expect(listProviders()).rejects.toThrow("Failed to load providers");
    });
  });

  describe("listAvailableProviders", () => {
    it("returns available provider array", async () => {
      const available = [{ id: "p1", name: "OpenRouter", providerType: "OpenRouter", model: "claude-3-5-sonnet" }];
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify(available), { status: 200 })
      );
      const result = await listAvailableProviders();
      expect(result[0].name).toBe("OpenRouter");
    });

    it("throws on failure", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response("{}", { status: 500 }));
      await expect(listAvailableProviders()).rejects.toThrow("Failed to load available providers");
    });
  });

  describe("createProvider", () => {
    it("returns created provider", async () => {
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify(makeProvider("p2")), { status: 200 })
      );
      const result = await createProvider({
        name: "My Provider",
        providerType: "OpenRouter",
        baseUrl: "https://openrouter.ai/api/v1",
        apiKey: "key",
        model: "claude-3-5-sonnet",
        contextWindowTokens: undefined,
      });
      expect(result.id).toBe("p2");
    });

    it("throws with server error message", async () => {
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify({ error: "Duplicate name" }), { status: 400 })
      );
      await expect(createProvider({ name: "x", providerType: "x", baseUrl: "x", apiKey: "x", model: "x" })).rejects.toThrow("Duplicate name");
    });
  });

  describe("updateProvider", () => {
    it("returns updated provider", async () => {
      const updated = { ...makeProvider(), name: "Renamed" };
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify(updated), { status: 200 })
      );
      const result = await updateProvider("p1", { name: "Renamed" });
      expect(result.name).toBe("Renamed");
    });

    it("throws with server error", async () => {
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify({ error: "Not found" }), { status: 404 })
      );
      await expect(updateProvider("bad", {})).rejects.toThrow("Not found");
    });
  });

  describe("deleteProvider", () => {
    it("resolves on success", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response(null, { status: 204 }));
      await expect(deleteProvider("p1")).resolves.toBeUndefined();
    });

    it("throws on failure", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response("{}", { status: 404 }));
      await expect(deleteProvider("bad")).rejects.toThrow("Failed to delete provider");
    });
  });

  describe("testProvider", () => {
    it("returns success result", async () => {
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify({ success: true, error: null }), { status: 200 })
      );
      const result = await testProvider({
        providerType: "OpenRouter",
        baseUrl: "https://openrouter.ai/api/v1",
        apiKey: "key",
        model: "claude-3-5-sonnet",
        contextWindowTokens: undefined,
      });
      expect(result.success).toBe(true);
    });

    it("throws with error message on failure", async () => {
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify({ error: "Invalid API key" }), { status: 400 })
      );
      await expect(testProvider({ providerType: "x", baseUrl: "x", apiKey: "x", model: "x", contextWindowTokens: undefined })).rejects.toThrow("Invalid API key");
    });
  });
});
