import type { Metadata } from "next";
import { Suspense } from "react";
import { LoginForm } from "@/components/auth/LoginForm";
import { Spinner } from "@/components/ui/Spinner";

export const metadata: Metadata = { title: "Log in" };

export default function LoginPage() {
  return (
    <>
      <h1 className="mb-6 text-2xl font-bold text-slate-900">Log in</h1>
      {/* LoginForm reads ?returnUrl with useSearchParams, which needs a Suspense boundary. */}
      <Suspense fallback={<Spinner />}>
        <LoginForm />
      </Suspense>
    </>
  );
}
