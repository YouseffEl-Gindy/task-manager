"use client";

import { useRouter } from "next/navigation";
import { useEffect, type ReactNode } from "react";
import { SERVER_UNAVAILABLE_MESSAGE } from "@/lib/api/client";
import { useAuth } from "@/lib/auth/AuthContext";
import { loginHref } from "@/lib/auth/safeReturnUrl";
import { ErrorMessage } from "@/components/ui/ErrorMessage";
import { Spinner } from "@/components/ui/Spinner";

function currentPath(): string {
  return `${window.location.pathname}${window.location.search}`;
}

/**
 * Blocks rendering until GET /auth/me confirms the session (SPEC 8.1).
 * - unauthorized (now or from any later call) -> /login?returnUrl=<current path>
 * - signed out (logout / account deleted) -> /login
 * - server unavailable -> retry message, no redirect (AC-ERR-5)
 */
export function AuthGuard({ children }: { children: ReactNode }) {
  const router = useRouter();
  const { status, checkSession } = useAuth();

  useEffect(() => {
    void checkSession();
  }, [checkSession]);

  useEffect(() => {
    if (status === "unauthenticated") {
      router.replace(loginHref(currentPath()));
    } else if (status === "signedOut") {
      router.replace("/login");
    }
  }, [status, router]);

  if (status === "authenticated") {
    return <>{children}</>;
  }
  if (status === "unavailable") {
    return (
      <div className="mx-auto w-full max-w-xl px-4 py-12">
        <ErrorMessage
          title="Can't reach the server"
          message={SERVER_UNAVAILABLE_MESSAGE}
          onRetry={() => void checkSession()}
        />
      </div>
    );
  }
  if (status === "unauthenticated" || status === "signedOut") {
    return <Spinner label="Redirecting to log in…" />;
  }
  return <Spinner label="Checking your session…" />;
}
