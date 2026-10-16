import type { ReactNode } from "react";
import { RedirectIfAuthenticated } from "@/components/auth/RedirectIfAuthenticated";

/** Layout for /login and /register: a centered card. Logged-in users are sent to /tasks. */
export default function PublicLayout({ children }: { children: ReactNode }) {
  return (
    <main id="main-content" className="flex flex-1 items-start justify-center px-4 py-12 sm:items-center">
      <RedirectIfAuthenticated />
      <div className="w-full max-w-md rounded-lg border border-slate-200 bg-white p-6 shadow-sm sm:p-8">
        <p className="mb-6 text-center text-lg font-bold text-slate-900">Task Manager</p>
        {children}
      </div>
    </main>
  );
}
