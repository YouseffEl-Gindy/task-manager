"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { getErrorMessage } from "@/lib/api/client";
import { useAuth } from "@/lib/auth/AuthContext";
import { Button } from "@/components/ui/Button";

export function LogoutButton() {
  const router = useRouter();
  const { logout } = useAuth();
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleClick() {
    setPending(true);
    setError(null);
    try {
      await logout();
      router.replace("/login");
    } catch (err) {
      setError(getErrorMessage(err));
      setPending(false);
    }
  }

  return (
    <div className="flex flex-col items-end gap-1">
      <Button variant="secondary" onClick={handleClick} isLoading={pending}>
        {pending ? "Logging out…" : "Log out"}
      </Button>
      {error ? (
        <p role="alert" className="max-w-xs text-right text-xs text-red-700">
          {error}
        </p>
      ) : null}
    </div>
  );
}
