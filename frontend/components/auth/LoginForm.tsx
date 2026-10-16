"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useState, type FormEvent } from "react";
import { login } from "@/lib/api/auth";
import { isApiErrorWithCode } from "@/lib/api/client";
import { useAuth } from "@/lib/auth/AuthContext";
import { safeReturnUrl } from "@/lib/auth/safeReturnUrl";
import { mapServerError } from "@/lib/forms/serverErrors";
import { collectErrors, hasErrors, validateExistingPassword, validateLoginEmail } from "@/lib/validation/rules";
import { Button } from "@/components/ui/Button";
import { FormError } from "@/components/ui/FormError";
import { PasswordField } from "@/components/ui/PasswordField";
import { TextField } from "@/components/ui/TextField";

type Field = "email" | "password";
const FIELDS: readonly Field[] = ["email", "password"];

export function LoginForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { setUser } = useAuth();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<Field, string>>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const errors = collectErrors<Field>({
      email: validateLoginEmail(email),
      password: validateExistingPassword(password),
    });
    setFieldErrors(errors);
    setFormError(null);
    if (hasErrors(errors)) {
      return;
    }
    setSubmitting(true);
    try {
      const user = await login({ email: email.trim(), password });
      setUser(user);
      router.replace(safeReturnUrl(searchParams.get("returnUrl")));
    } catch (error) {
      if (isApiErrorWithCode(error, "invalid_credentials")) {
        // Shown on the form; this 401 never triggers the login redirect.
        setFormError(error.message);
      } else {
        const mapped = mapServerError(error, FIELDS);
        setFieldErrors(mapped.fieldErrors);
        setFormError(mapped.formError);
      }
      setSubmitting(false);
    }
  }

  return (
    <form noValidate onSubmit={handleSubmit} className="flex flex-col gap-4" aria-label="Log in">
      <FormError message={formError} />
      <TextField
        label="Email"
        type="email"
        name="email"
        autoComplete="email"
        required
        value={email}
        onChange={(e) => setEmail(e.target.value)}
        error={fieldErrors.email}
      />
      <PasswordField
        label="Password"
        name="password"
        autoComplete="current-password"
        required
        value={password}
        onChange={(e) => setPassword(e.target.value)}
        error={fieldErrors.password}
      />
      <Button type="submit" isLoading={submitting}>
        {submitting ? "Logging in…" : "Log in"}
      </Button>
      <p className="text-center text-sm text-slate-700">
        No account yet?{" "}
        <Link href="/register" className="font-medium text-blue-700 underline hover:text-blue-900">
          Create one
        </Link>
      </p>
    </form>
  );
}
