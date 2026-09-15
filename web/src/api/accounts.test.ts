import { describe, it, expect, vi, beforeEach, type Mock } from "vitest";
import { listAccounts, saveAccount, deleteAccount, getLinkedInAuthUrl, submitLinkedInCallback } from "./accounts";

vi.mock("./client", () => ({
  apiFetch: vi.fn(),
}));

import { apiFetch } from "./client";
const mockApiFetch = apiFetch as Mock;

const makeAccount = (id = "a1") => ({
  id,
  platform: "Bluesky",
  handle: "alice.bsky.social",
  status: "Active",
  createdAt: "2026-01-01",
  updatedAt: "2026-01-01",
});

describe("accounts API", () => {
  beforeEach(() => {
    mockApiFetch.mockReset();
  });

  describe("listAccounts", () => {
    it("returns account array", async () => {
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify([makeAccount()]), { status: 200 })
      );
      const result = await listAccounts();
      expect(result).toHaveLength(1);
      expect(result[0].handle).toBe("alice.bsky.social");
    });

    it("throws on failure", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response("{}", { status: 500 }));
      await expect(listAccounts()).rejects.toThrow("Failed to fetch accounts");
    });
  });

  describe("saveAccount", () => {
    it("sends POST and resolves on success", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response(null, { status: 204 }));
      await expect(
        saveAccount({ platform: "Bluesky", handle: "alice.bsky.social", appPassword: "pw" })
      ).resolves.toBeUndefined();
      expect(mockApiFetch).toHaveBeenCalledWith(
        "/api/accounts",
        expect.objectContaining({ method: "POST" })
      );
    });

    it("throws on failure", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response("{}", { status: 400 }));
      await expect(
        saveAccount({ platform: "Bluesky", handle: "bad" })
      ).rejects.toThrow("Failed to save account");
    });
  });

  describe("deleteAccount", () => {
    it("sends DELETE and resolves on success", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response(null, { status: 204 }));
      await expect(deleteAccount("a1")).resolves.toBeUndefined();
      expect(mockApiFetch).toHaveBeenCalledWith(
        "/api/accounts/a1",
        expect.objectContaining({ method: "DELETE" })
      );
    });

    it("throws on failure", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response("{}", { status: 404 }));
      await expect(deleteAccount("bad")).rejects.toThrow("Failed to delete account");
    });
  });

  describe("getLinkedInAuthUrl", () => {
    it("returns authUrl on success for personal", async () => {
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify({ authUrl: "https://linkedin.com/auth?state=personal" }), { status: 200 })
      );
      const res = await getLinkedInAuthUrl("personal");
      expect(res.authUrl).toBe("https://linkedin.com/auth?state=personal");
      expect(mockApiFetch).toHaveBeenCalledWith("/api/accounts/linkedin/auth-url?type=personal");
    });

    it("returns authUrl on success for company", async () => {
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify({ authUrl: "https://linkedin.com/auth?state=company" }), { status: 200 })
      );
      const res = await getLinkedInAuthUrl("company");
      expect(res.authUrl).toBe("https://linkedin.com/auth?state=company");
      expect(mockApiFetch).toHaveBeenCalledWith("/api/accounts/linkedin/auth-url?type=company");
    });

    it("throws detailed server error message on failure", async () => {
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify({ error: "LinkedIn client ID or redirect URI is not configured." }), { status: 400 })
      );
      await expect(getLinkedInAuthUrl()).rejects.toThrow("LinkedIn client ID or redirect URI is not configured.");
    });
  });

  describe("submitLinkedInCallback", () => {
    it("returns personal and organization handles on success", async () => {
      const responseData = {
        personalHandle: "urn:li:person:user123",
        organizationHandles: ["Acme Corp (urn:li:organization:998877)"],
      };
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify(responseData), { status: 200 })
      );
      const res = await submitLinkedInCallback("auth-code", "company");
      expect(res.personalHandle).toBe("urn:li:person:user123");
      expect(res.organizationHandles).toEqual(["Acme Corp (urn:li:organization:998877)"]);
      expect(mockApiFetch).toHaveBeenCalledWith(
        "/api/accounts/linkedin/callback",
        expect.objectContaining({
          method: "POST",
          body: JSON.stringify({ code: "auth-code", accountType: "company" }),
        })
      );
    });

    it("throws on callback failure", async () => {
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify({ error: "Code expired" }), { status: 400 })
      );
      await expect(submitLinkedInCallback("bad-code")).rejects.toThrow("Code expired");
    });
  });
});
