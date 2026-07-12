import { describe, it, expect, vi, beforeEach, afterEach, type Mock } from "vitest";
import { useAuthStore } from "../store/authStore";

vi.mock("../store/authStore", () => ({
  useAuthStore: {
    getState: vi.fn(),
  },
}));

const mockSetTokens = vi.fn();
const mockLogout = vi.fn();

function mockAuthState(overrides: {
  accessToken?: string | null;
  refreshToken?: string | null;
} = {}) {
  (useAuthStore.getState as Mock).mockReturnValue({
    accessToken: overrides.accessToken ?? null,
    refreshToken: overrides.refreshToken ?? null,
    setTokens: mockSetTokens,
    logout: mockLogout,
  });
}

describe("apiFetch", () => {
  let fetchMock: Mock;
  let apiFetch: (url: string, options?: RequestInit) => Promise<Response>;

  beforeEach(async () => {
    vi.resetModules();
    fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
    mockSetTokens.mockReset();
    mockLogout.mockReset();
    const module = await import("./client");
    apiFetch = module.apiFetch;
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("sends request without Authorization header when no access token", async () => {
    mockAuthState({ accessToken: null, refreshToken: null });
    fetchMock.mockResolvedValueOnce(new Response("{}", { status: 200 }));

    await apiFetch("/api/test");

    const [, options] = fetchMock.mock.calls[0];
    expect((options.headers as Record<string, string>)["Authorization"]).toBeUndefined();
  });

  it("injects Bearer Authorization header when access token is present", async () => {
    mockAuthState({ accessToken: "my-token", refreshToken: null });
    fetchMock.mockResolvedValueOnce(new Response("{}", { status: 200 }));

    await apiFetch("/api/test");

    const [, options] = fetchMock.mock.calls[0];
    expect((options.headers as Record<string, string>)["Authorization"]).toBe("Bearer my-token");
  });

  it("sets Content-Type application/json when body is not FormData", async () => {
    mockAuthState();
    fetchMock.mockResolvedValueOnce(new Response("{}", { status: 200 }));

    await apiFetch("/api/test", { method: "POST", body: JSON.stringify({}) });

    const [, options] = fetchMock.mock.calls[0];
    expect((options.headers as Record<string, string>)["Content-Type"]).toBe("application/json");
  });

  it("does not set Content-Type when body is FormData", async () => {
    mockAuthState();
    fetchMock.mockResolvedValueOnce(new Response("{}", { status: 200 }));

    await apiFetch("/api/test", { method: "POST", body: new FormData() });

    const [, options] = fetchMock.mock.calls[0];
    expect((options.headers as Record<string, string>)["Content-Type"]).toBeUndefined();
  });

  it("on 401 with refresh token, calls /api/auth/refresh and retries with new token", async () => {
    mockAuthState({ accessToken: "expired", refreshToken: "rt" });
    fetchMock
      .mockResolvedValueOnce(new Response("{}", { status: 401 }))
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ accessToken: "new-token" }), { status: 200 })
      )
      .mockResolvedValueOnce(new Response("{}", { status: 200 }));

    const res = await apiFetch("/api/test");

    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect(fetchMock.mock.calls[1][0]).toBe("/api/auth/refresh");
    expect(mockSetTokens).toHaveBeenCalledWith("new-token", "rt");
    const [, retryOptions] = fetchMock.mock.calls[2];
    expect((retryOptions.headers as Record<string, string>)["Authorization"]).toBe("Bearer new-token");
    expect(res.status).toBe(200);
  });

  it("on 401 when refresh fails, calls logout", async () => {
    mockAuthState({ accessToken: "expired", refreshToken: "rt" });
    mockLogout.mockResolvedValue(undefined);
    fetchMock
      .mockResolvedValueOnce(new Response("{}", { status: 401 }))
      .mockResolvedValueOnce(new Response("{}", { status: 401 }));

    await apiFetch("/api/test");

    expect(mockLogout).toHaveBeenCalled();
  });

  it("on 401 without refresh token, returns the 401 response without retrying", async () => {
    mockAuthState({ accessToken: "expired", refreshToken: null });
    fetchMock.mockResolvedValueOnce(new Response("{}", { status: 401 }));

    const res = await apiFetch("/api/test");

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(res.status).toBe(401);
  });
});
