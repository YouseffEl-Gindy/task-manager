"use client";

import { useRouter } from "next/navigation";
import { useEffect } from "react";
import { useAuth } from "@/lib/auth/AuthContext";
import { DEFAULT_AFTER_LOGIN } from "@/lib/auth/safeReturnUrl";

/** On /login and /register: a user who already has a session is sent to /tasks (SPEC 8.1). Renders nothing. */
export function RedirectIfAuthenticated() {
  const router = useRouter();
  const { checkSession } = useAuth();

  useEffect(() => {
    let active = true;
    void checkSession().then((status) => {
      if (active && status === "authenticated") {
        router.replace(DEFAULT_AFTER_LOGIN);
      }
    });
    return () => {
      active = false;
    };
  }, [checkSession, router]);

  return null;
}
