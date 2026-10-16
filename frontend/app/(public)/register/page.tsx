import type { Metadata } from "next";
import { RegisterForm } from "@/components/auth/RegisterForm";

export const metadata: Metadata = { title: "Create account" };

export default function RegisterPage() {
  return (
    <>
      <h1 className="mb-6 text-2xl font-bold text-slate-900">Create account</h1>
      <RegisterForm />
    </>
  );
}
