"use client";

import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import type { UserDto } from "@/types/api";
import { getCurrentUser, logout as apiLogout } from "@/lib/api/auth";
import { ApiResponseError, onUnauthorized } from "@/lib/api/client";

/**
 * - `unknown`: not checked yet.
 * - `loading`: GET /auth/me in flight with no confirmed user.
 * - `authenticated`: a confirmed session.
 * - `unauthenticated`: no session or it expired (a protected page redirects with returnUrl).
 * - `signedOut`: the user logged out or deleted the account (redirect to plain /login).
 * - `unavailable`: the server could not be reached (show the retry message, don't redirect).
 */
export type AuthStatus = "unknown" | "loading" | "authenticated" | "unauthenticated" | "signedOut" | "unavailable";

export interface AuthContextValue {
  status: AuthStatus;
  user: UserDto | null;
  /** Calls GET /auth/me and updates the state. Resolves with the resulting status. */
  checkSession: () => Promise<AuthStatus>;
  /** Stores the user returned by login, register, or a profile update. */
  setUser: (user: UserDto) => void;
  /** POST /auth/logout, then marks the user as signed out. Rejects (and stays signed in) if the call fails. */
  logout: () => Promise<void>;
  /** Marks the user as signed out without calling the API (e.g. after account deletion). */
  markSignedOut: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

interface AuthState {
  status: AuthStatus;
  user: UserDto | null;
}

export function AuthProvider({ children, initialUser = null }: { children: ReactNode; initialUser?: UserDto | null }) {
  const [state, setState] = useState<AuthState>(
    initialUser ? { status: "authenticated", user: initialUser } : { status: "unknown", user: null },
  );
  const requestId = useRef(0);

  useEffect(
    () =>
      onUnauthorized(() => {
        requestId.current += 1;
        setState((current) =>
          current.status === "signedOut" ? current : { status: "unauthenticated", user: null },
        );
      }),
    [],
  );

  const checkSession = useCallback(async (): Promise<AuthStatus> => {
    const id = ++requestId.current;
    setState((current) => (current.status === "authenticated" ? current : { status: "loading", user: null }));
    let next: AuthState;
    try {
      const user = await getCurrentUser();
      next = { status: "authenticated", user };
    } catch (error) {
      next =
        error instanceof ApiResponseError && error.status === 401
          ? { status: "unauthenticated", user: null }
          : { status: "unavailable", user: null };
    }
    if (id === requestId.current) {
      setState((current) => {
        // A background re-check of a confirmed session keeps the user when the server is only unreachable.
        if (next.status === "unavailable" && current.status === "authenticated") {
          return current;
        }
        return next;
      });
    }
    return next.status;
  }, []);

  const setUser = useCallback((user: UserDto) => {
    requestId.current += 1;
    setState({ status: "authenticated", user });
  }, []);

  const markSignedOut = useCallback(() => {
    requestId.current += 1;
    setState({ status: "signedOut", user: null });
  }, []);

  const logout = useCallback(async () => {
    // If this throws (server unavailable), the cookie may still be valid, so stay signed in and let the caller show the error.
    await apiLogout();
    markSignedOut();
  }, [markSignedOut]);

  const value = useMemo<AuthContextValue>(
    () => ({ status: state.status, user: state.user, checkSession, setUser, logout, markSignedOut }),
    [state, checkSession, setUser, logout, markSignedOut],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const value = useContext(AuthContext);
  if (value === null) {
    throw new Error("useAuth must be used inside <AuthProvider>.");
  }
  return value;
}
