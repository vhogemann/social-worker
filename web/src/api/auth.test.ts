import { describe, it, expect, vi, beforeEach, afterEach, type Mock } from "vitest";
import {
  login,
  refresh,
  logout,
  getMe,
  changePassword,
  listUsers,
  createUser,
  updateUser,
  resetUserPassword,
  setPreferredProvider,
} from "./auth";

vi.mock("./client", () => ({
  apiFetch: vi.fn(),
}));

import { apiFetch } from "./client";
const mockApiFetch = apiFetch as Mock;

describe("auth API", () => {
  let fetchMock: Mock;

  beforeEach(() => {
    fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
    mockApiFetch.mockReset();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  describe("login", () => {
    it("returns LoginResponse on success", async () => {
      const payload = {
        accessToken: "at",
        refreshToken: "rt",
        expiresAt: "2026-01-01",
        user: { id: "1", username: "alice", email: "alice@example.com", role: "Admin" },
      };
      fetchMock.mockResolvedValueOnce(
        new Response(JSON.stringify(payload), { status: 200 })
      );

      const result = await login("alice", "password");

      expect(fetchMock).toHaveBeenCalledWith("/api/auth/login", expect.objectContaining({ method: "POST" }));
      expect(result.accessToken).toBe("at");
      expect(result.user.username).toBe("alice");
    });

    it("throws on non-ok response", async () => {
      fetchMock.mockResolvedValueOnce(new Response("{}", { status: 401 }));
      await expect(login("alice", "wrong")).rejects.toThrow("Invalid credentials");
    });
  });

  describe("refresh", () => {
    it("returns RefreshResponse on success", async () => {
      const payload = { accessToken: "new-at", expiresAt: "2026-01-01" };
      fetchMock.mockResolvedValueOnce(
        new Response(JSON.stringify(payload), { status: 200 })
      );

      const result = await refresh("my-refresh-token");

      expect(result.accessToken).toBe("new-at");
    });

    it("throws on non-ok response", async () => {
      fetchMock.mockResolvedValueOnce(new Response("{}", { status: 401 }));
      await expect(refresh("bad-token")).rejects.toThrow("Session expired");
    });
  });

  describe("logout", () => {
    it("calls the logout endpoint with refresh token", async () => {
      fetchMock.mockResolvedValueOnce(new Response(null, { status: 204 }));

      await logout("my-rt");

      expect(fetchMock).toHaveBeenCalledWith(
        "/api/auth/logout",
        expect.objectContaining({
          method: "POST",
          body: JSON.stringify({ refreshToken: "my-rt" }),
        })
      );
    });
  });

  describe("getMe", () => {
    it("returns UserDto on success", async () => {
      const user = { id: "1", username: "alice", email: "alice@example.com", role: "Admin" };
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify(user), { status: 200 })
      );

      const result = await getMe();

      expect(result.username).toBe("alice");
    });

    it("throws Unauthorized on non-ok", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response("{}", { status: 401 }));
      await expect(getMe()).rejects.toThrow("Unauthorized");
    });
  });

  describe("changePassword", () => {
    it("resolves on success", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response(null, { status: 204 }));
      await expect(changePassword("old", "new")).resolves.toBeUndefined();
    });

    it("throws with error message on failure", async () => {
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify({ error: "Wrong password" }), { status: 400 })
      );
      await expect(changePassword("wrong", "new")).rejects.toThrow("Wrong password");
    });
  });

  describe("listUsers", () => {
    it("returns user array", async () => {
      const users = [{ id: "1", username: "alice", email: "a@b.com", role: "Admin" }];
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify(users), { status: 200 })
      );

      const result = await listUsers();
      expect(result).toHaveLength(1);
    });

    it("throws on failure", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response("{}", { status: 500 }));
      await expect(listUsers()).rejects.toThrow("Failed to load users");
    });
  });

  describe("createUser", () => {
    it("returns created user", async () => {
      const user = { id: "2", username: "bob", email: "b@c.com", role: "User" };
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify(user), { status: 200 })
      );

      const result = await createUser({ username: "bob", email: "b@c.com", password: "pw" });
      expect(result.username).toBe("bob");
    });

    it("throws with server error message", async () => {
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify({ error: "Username taken" }), { status: 400 })
      );
      await expect(createUser({ username: "alice" })).rejects.toThrow("Username taken");
    });
  });

  describe("updateUser", () => {
    it("returns updated user", async () => {
      const user = { id: "1", username: "alice2", email: "a@b.com", role: "Admin" };
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify(user), { status: 200 })
      );

      const result = await updateUser("1", { username: "alice2" });
      expect(result.username).toBe("alice2");
    });

    it("throws on failure", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response("{}", { status: 400 }));
      await expect(updateUser("1", { username: "x" })).rejects.toThrow("Failed to update user");
    });
  });

  describe("resetUserPassword", () => {
    it("resolves on success", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response(null, { status: 204 }));
      await expect(resetUserPassword("1", { newPassword: "abc" })).resolves.toBeUndefined();
    });

    it("throws on failure", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response("{}", { status: 400 }));
      await expect(resetUserPassword("1", { newPassword: "abc" })).rejects.toThrow("Failed to reset password");
    });
  });

  describe("setPreferredProvider", () => {
    it("resolves on success", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response(null, { status: 204 }));
      await expect(setPreferredProvider("provider-1")).resolves.toBeUndefined();
    });

    it("throws on failure", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response("{}", { status: 400 }));
      await expect(setPreferredProvider(null)).rejects.toThrow("Failed to update preferred provider");
    });
  });
});
