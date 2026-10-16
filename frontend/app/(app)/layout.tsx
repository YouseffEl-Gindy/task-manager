import type { ReactNode } from "react";
import { AuthGuard } from "@/components/auth/AuthGuard";
import { AppHeader } from "@/components/layout/AppHeader";

/** Protected area: AuthGuard confirms the session before anything renders. */
export default function ProtectedLayout({ children }: { children: ReactNode }) {
  return (
    <AuthGuard>
      <AppHeader />
      <main id="main-content" className="mx-auto w-full max-w-5xl flex-1 px-4 py-8">
        {children}
      </main>
    </AuthGuard>
  );
}
