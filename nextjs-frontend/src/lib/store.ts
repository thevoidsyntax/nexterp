import { create } from 'zustand';
import { persist } from 'zustand/middleware';

export interface User {
  id: string;
  username: string;
  email: string;
  firstName: string;
  lastName: string;
  fullName: string;
  organizationId: string;
  isActive: boolean;
  isSuperAdmin: boolean;
  roles: string[];
}

interface AuthState {
  user: User | null;
  token: string | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (user: User, token: string) => void;
  logout: () => void;
  setLoading: (loading: boolean) => void;
  updateUser: (patch: Partial<User>) => void;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      user: null,
      token: null,
      isAuthenticated: false,
      isLoading: true,

      // The access/refresh tokens live only in the HttpOnly cookies the API sets on
      // login (see AuthController) — never in localStorage or this store's persisted
      // state, so an XSS bug can't read them via `document.cookie`/localStorage the
      // way it could when this store also mirrored the token client-side. `token` is
      // kept on the in-memory state only for backward compatibility with existing
      // callers; it is not persisted or used to build an Authorization header.
      login: (user, token) => {
        set({ user, token, isAuthenticated: true, isLoading: false });
      },

      // Only clears local UI state. Call authApi.logout() first to actually clear
      // the HttpOnly cookies server-side — this alone cannot do that.
      logout: () => {
        set({ user: null, token: null, isAuthenticated: false, isLoading: false });
      },

      setLoading: (isLoading) => set({ isLoading }),

      updateUser: (patch) => {
        const current = get().user;
        if (!current) return;
        set({ user: { ...current, ...patch } });
      },
    }),
    {
      name: 'nexterp-auth',
      partialize: (state) => ({
        user: state.user,
        isAuthenticated: state.isAuthenticated,
      }),
    }
  )
);
