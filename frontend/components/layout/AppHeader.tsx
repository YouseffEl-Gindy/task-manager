"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useAuth } from "@/lib/auth/AuthContext";
import { LogoutButton } from "@/components/auth/LogoutButton";

const NAV_LINKS = [
  { href: "/tasks", label: "Tasks" },
  { href: "/account", label: "Account" },
] as const;

function isActive(pathname: string, href: string): boolean {
  return pathname === href || pathname.startsWith(`${href}/`);
}

export function AppHeader() {
  const pathname = usePathname();
  const { user } = useAuth();

  return (
    <header className="border-b border-slate-200 bg-white">
      <div className="mx-auto flex w-full max-w-5xl flex-wrap items-center justify-between gap-3 px-4 py-3">
        <div className="flex flex-wrap items-center gap-4">
          <Link href="/tasks" className="text-lg font-bold text-slate-900">
            Task Manager
          </Link>
          <nav aria-label="Main">
            <ul className="flex gap-1">
              {NAV_LINKS.map((link) => {
                const active = isActive(pathname, link.href);
                return (
                  <li key={link.href}>
                    <Link
                      href={link.href}
                      aria-current={active ? "page" : undefined}
                      className={`rounded-md px-3 py-2 text-sm font-medium focus-visible:outline-2 focus-visible:outline-blue-700 ${
                        active ? "bg-slate-100 text-slate-900" : "text-slate-700 hover:bg-slate-50"
                      }`}
                    >
                      {link.label}
                    </Link>
                  </li>
                );
              })}
            </ul>
          </nav>
        </div>
        <div className="flex items-center gap-3">
          {user ? (
            <p className="text-sm text-slate-700">
              <span className="sr-only">Signed in as </span>
              <span data-testid="current-user-name" className="font-medium text-slate-900">
                {user.displayName}
              </span>
            </p>
          ) : null}
          <LogoutButton />
        </div>
      </div>
    </header>
  );
}
