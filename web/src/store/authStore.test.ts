import { describe, it, expect, vi, beforeEach, type Mock } from "vitest";
import { useAuthStore } from "./authStore";

vi.mock("../api/auth", () => ({
  login: vi.fn(),
  logout: vi.fn(),
  refresh: vi.fn(),
  getMe: vi.fn(),
}));

import * as authApi from "../api/auth";
const mockLogin = authApi.login as Mock;
const mockLogout = authApi.logout as Mock;
const mockRefresh = authApi.refresh as Mock;
const mockGetMe = authApi.getMe as Mock;

const mockUser = { id: "1", username: "alice", email: "alice@example.com", role: "Admin" };

function resetStore() {
  useAuthStore.setState({
    user: null,
    accessToken: null,
    refreshToken: null,
    isAuthenticated: false,
    initialized: false,
  });
}

describe("authStore", () => {
  beforeEach(() => {
    localStorage.clear();
    resetStore();
    vi.clearAllMocks();
  });

  describe("setTokens", () => {
    it("stores tokens in localStorage and updates state", () => {
      useAuthStore.getState().setTokens("at-1", "rt-1");

      expect(localStorage.getItem("sw_access_token")).toBe("at-1");
      expect(localStorage.getItem("sw_refresh_token")).toBe("rt-1");
      expect(useAuthStore.getState().accessToken).toBe("at-1");
      expect(useAuthStore.getState().refreshToken).toBe("rt-1");
      expect(useAuthStore.getState().isAuthenticated).toBe(true);
    });

    it("removes tokens from localStorage when set to null", () => {
      localStorage.setItem("sw_access_token", "old");
      localStorage.setItem("sw_refresh_token", "old-rt");

      useAuthStore.getState().setTokens(null, null);

      expect(localStorage.getItem("sw_access_token")).toBeNull();
      expect(localStorage.getItem("sw_refresh_token")).toBeNull();
      expect(useAuthStore.getState().isAuthenticated).toBe(false);
    });
  });

  describe("login", () => {
    it("calls apiLogin, sets tokens in localStorage, sets user, and marks isAuthenticated", async () => {
      mockLogin.mockResolvedValueOnce({ accessToken: "at", refreshToken: "rt", user: mockUser });

      await useAuthStore.getState().login("alice", "pw");

      expect(mockLogin).toHaveBeenCalledWith("alice", "pw");
      expect(localStorage.getItem("sw_access_token")).toBe("at");
      expect(localStorage.getItem("sw_refresh_token")).toBe("rt");
      const state = useAuthStore.getState();
      expect(state.user).toEqual(mockUser);
      expect(state.isAuthenticated).toBe(true);
    });

    it("propagates error when apiLogin throws", async () => {
      mockLogin.mockRejectedValueOnce(new Error("Invalid credentials"));
      await expect(useAuthStore.getState().login("bad", "bad")).rejects.toThrow("Invalid credentials");
    });
  });

  describe("logout", () => {
    it("calls apiLogout, clears tokens and user", async () => {
      useAuthStore.setState({ accessToken: "at", refreshToken: "rt", user: mockUser, isAuthenticated: true });
      localStorage.setItem("sw_access_token", "at");
      localStorage.setItem("sw_refresh_token", "rt");
      mockLogout.mockResolvedValueOnce(undefined);

      await useAuthStore.getState().logout();

      expect(mockLogout).toHaveBeenCalledWith("rt");
      expect(localStorage.getItem("sw_access_token")).toBeNull();
      expect(localStorage.getItem("sw_refresh_token")).toBeNull();
      const state = useAuthStore.getState();
      expect(state.user).toBeNull();
      expect(state.isAuthenticated).toBe(false);
    });

    it("still clears state even when apiLogout throws", async () => {
      useAuthStore.setState({ accessToken: "at", refreshToken: "rt", user: mockUser, isAuthenticated: true });
      mockLogout.mockRejectedValueOnce(new Error("Network error"));

      await useAuthStore.getState().logout();

      const state = useAuthStore.getState();
      expect(state.user).toBeNull();
      expect(state.isAuthenticated).toBe(false);
    });

    it("skips apiLogout when there is no refresh token", async () => {
      useAuthStore.setState({ accessToken: "at", refreshToken: null, user: mockUser, isAuthenticated: true });

      await useAuthStore.getState().logout();

      expect(mockLogout).not.toHaveBeenCalled();
    });
  });

  describe("initialize", () => {
    it("sets initialized=true and isAuthenticated=false when no tokens", async () => {
      await useAuthStore.getState().initialize();

      const state = useAuthStore.getState();
      expect(state.initialized).toBe(true);
      expect(state.isAuthenticated).toBe(false);
    });

    it("with valid access token calls getMe and sets user", async () => {
      useAuthStore.setState({ accessToken: "at", refreshToken: "rt" });
      mockGetMe.mockResolvedValueOnce(mockUser);

      await useAuthStore.getState().initialize();

      const state = useAuthStore.getState();
      expect(state.user).toEqual(mockUser);
      expect(state.isAuthenticated).toBe(true);
      expect(state.initialized).toBe(true);
    });

    it("with only refresh token, refreshes then calls getMe", async () => {
      useAuthStore.setState({ accessToken: null, refreshToken: "rt" });
      mockRefresh.mockResolvedValueOnce({ accessToken: "new-at", expiresAt: "" });
      mockGetMe.mockResolvedValueOnce(mockUser);

      await useAuthStore.getState().initialize();

      expect(mockRefresh).toHaveBeenCalledWith("rt");
      const state = useAuthStore.getState();
      expect(state.user).toEqual(mockUser);
      expect(state.isAuthenticated).toBe(true);
    });

    it("with only refresh token, clears state when refresh fails", async () => {
      useAuthStore.setState({ accessToken: null, refreshToken: "rt" });
      mockRefresh.mockRejectedValueOnce(new Error("Session expired"));

      await useAuthStore.getState().initialize();

      const state = useAuthStore.getState();
      expect(state.isAuthenticated).toBe(false);
      expect(state.initialized).toBe(true);
    });

    it("retries via refresh when getMe fails with access+refresh token", async () => {
      useAuthStore.setState({ accessToken: "expired-at", refreshToken: "rt" });
      mockGetMe
        .mockRejectedValueOnce(new Error("Unauthorized"))
        .mockResolvedValueOnce(mockUser);
      mockRefresh.mockResolvedValueOnce({ accessToken: "new-at", expiresAt: "" });

      await useAuthStore.getState().initialize();

      const state = useAuthStore.getState();
      expect(state.user).toEqual(mockUser);
      expect(state.isAuthenticated).toBe(true);
    });

    it("clears state when getMe fails and refresh also fails", async () => {
      useAuthStore.setState({ accessToken: "expired-at", refreshToken: "rt" });
      mockGetMe.mockRejectedValueOnce(new Error("Unauthorized"));
      mockRefresh.mockRejectedValueOnce(new Error("Session expired"));

      await useAuthStore.getState().initialize();

      const state = useAuthStore.getState();
      expect(state.isAuthenticated).toBe(false);
      expect(state.initialized).toBe(true);
    });
  });

  describe("updatePreferredProviderId", () => {
    it("updates preferredProviderId on the user", () => {
      useAuthStore.setState({ user: { ...mockUser } });
      useAuthStore.getState().updatePreferredProviderId("provider-1");
      const state = useAuthStore.getState();
      expect(state.user?.preferredProviderId).toBe("provider-1");
    });

    it("handles null providerId", () => {
      useAuthStore.setState({ user: { ...mockUser, preferredProviderId: "old" } });
      useAuthStore.getState().updatePreferredProviderId(null);
      expect(useAuthStore.getState().user?.preferredProviderId).toBeNull();
    });
  });
});
