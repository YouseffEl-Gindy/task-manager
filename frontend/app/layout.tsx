import type { Metadata } from "next";
import { AuthProvider } from "@/lib/auth/AuthContext";
import "./globals.css";

export const metadata: Metadata = {
  title: {
    default: "Task Manager",
    template: "%s · Task Manager",
  },
  description: "A personal task manager.",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="en" className="h-full antialiased">
      <body className="flex min-h-full flex-col bg-slate-50 text-slate-900">
        <a
          href="#main-content"
          className="sr-only focus:not-sr-only focus:absolute focus:left-2 focus:top-2 focus:z-50 focus:rounded focus:bg-white focus:px-3 focus:py-2 focus:shadow"
        >
          Skip to main content
        </a>
        <AuthProvider>{children}</AuthProvider>
      </body>
    </html>
  );
}
