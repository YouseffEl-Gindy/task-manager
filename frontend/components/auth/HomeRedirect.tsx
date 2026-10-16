"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { SERVER_UNAVAILABLE_MESSAGE } from "@/lib/api/client";
import { useAuth } from "@/lib/auth/AuthContext";
import { ErrorMessage } from "@/components/ui/ErrorMessage";
import { Spinner } from "@/components/ui/Spinner";

/** `/`: goes to /tasks if GET /auth/me succeeds, otherwise to /login. */
export function HomeRedirect() {
  const router = useRouter();
  const { checkSession } = useAuth();
  const [attempt, setAttempt] = useState(0);
  const [unavailable, setUnavailable] = useState(false);

  useEffect(() => {
    let active = true;
    void checkSession().then((status) => {
      if (!active) return;
      if (status === "authenticated") {
        router.replace("/tasks");
      } else if (status === "unavailable") {
        setUnavailable(true);
      } else {
        router.replace("/login");
      }
    });
    return () => {
      active = false;
    };
  }, [attempt, checkSession, router]);

  if (unavailable) {
    return (
      <ErrorMessage
        title="Can't reach the server"
        message={SERVER_UNAVAILABLE_MESSAGE}
        onRetry={() => {
          setUnavailable(false);
          setAttempt((n) => n + 1);
        }}
      />
    );
  }
  return <Spinner label="Loading…" />;
}
