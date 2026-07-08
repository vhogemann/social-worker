import { create } from "zustand";
import { login as apiLogin, getMe, type UserDto, refresh as apiRefresh, logout as apiLogout } from "../api/auth";

interface AuthState {
  user: UserDto | null;
  accessToken: string | null;
  refreshToken: string | null;
  isAuthenticated: boolean;
  initialized: boolean;
  login: (emailOrUsername: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  initialize: () => Promise<void>;
  setTokens: (accessToken: string | null, refreshToken: string | null) => void;
  updatePreferredProviderId: (providerId: string | null) => void;
}

export const useAuthStore = create<AuthState>((set, get) => ({
  user: null,
  accessToken: localStorage.getItem("sw_access_token"),
  refreshToken: localStorage.getItem("sw_refresh_token"),
  isAuthenticated: false,
  initialized: false,

  setTokens: (accessToken, refreshToken) => {
    if (accessToken) {
      localStorage.setItem("sw_access_token", accessToken);
    } else {
      localStorage.removeItem("sw_access_token");
    }

    if (refreshToken) {
      localStorage.setItem("sw_refresh_token", refreshToken);
    } else {
      localStorage.removeItem("sw_refresh_token");
    }

    set({ accessToken, refreshToken, isAuthenticated: !!accessToken });
  },

  login: async (emailOrUsername, password) => {
    const res = await apiLogin(emailOrUsername, password);
    get().setTokens(res.accessToken, res.refreshToken);
    set({ user: res.user, isAuthenticated: true });
  },

  logout: async () => {
    const rt = get().refreshToken;
    if (rt) {
      try {
        await apiLogout(rt);
      } catch {
        // Ignored
      }
    }
    get().setTokens(null, null);
    set({ user: null, isAuthenticated: false });
  },

  initialize: async () => {
    const at = get().accessToken;
    const rt = get().refreshToken;

    if (!at && !rt) {
      set({ isAuthenticated: false, user: null, initialized: true });
      return;
    }

    if (!at && rt) {
      try {
        const res = await apiRefresh(rt);
        get().setTokens(res.accessToken, rt);
      } catch {
        get().setTokens(null, null);
        set({ initialized: true });
        return;
      }
    }

    try {
      const user = await getMe();
      set({ user, isAuthenticated: true, initialized: true });
    } catch {
      if (rt) {
        try {
          const res = await apiRefresh(rt);
          get().setTokens(res.accessToken, rt);
          const user = await getMe();
          set({ user, isAuthenticated: true, initialized: true });
        } catch {
          get().setTokens(null, null);
          set({ initialized: true });
        }
      } else {
        get().setTokens(null, null);
        set({ initialized: true });
      }
    }
  },

  updatePreferredProviderId: (providerId) => {
    const user = get().user;
    if (user) {
      set({ user: { ...user, preferredProviderId: providerId } });
    }
  }
}));
