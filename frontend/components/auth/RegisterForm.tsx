"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import { register } from "@/lib/api/auth";
import { isApiErrorWithCode } from "@/lib/api/client";
import { useAuth } from "@/lib/auth/AuthContext";
import { DEFAULT_AFTER_LOGIN } from "@/lib/auth/safeReturnUrl";
import { mapServerError } from "@/lib/forms/serverErrors";
import {
  DISPLAY_NAME_MAX,
  EMAIL_MAX,
  PASSWORD_MAX,
  collectErrors,
  hasErrors,
  validateDisplayName,
  validateEmail,
  validateNewPassword,
  validatePasswordConfirmation,
} from "@/lib/validation/rules";
import { Button } from "@/components/ui/Button";
import { FormError } from "@/components/ui/FormError";
import { PasswordField } from "@/components/ui/PasswordField";
import { TextField } from "@/components/ui/TextField";

type Field = "displayName" | "email" | "password" | "confirmPassword";
const SERVER_FIELDS: readonly Field[] = ["displayName", "email", "password"];

export function RegisterForm() {
  const router = useRouter();
  const { setUser } = useAuth();
  const [displayName, setDisplayName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<Field, string>>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const errors = collectErrors<Field>({
      displayName: validateDisplayName(displayName),
      email: validateEmail(email),
      password: validateNewPassword(password),
      confirmPassword: validatePasswordConfirmation(password, confirmPassword),
    });
    setFieldErrors(errors);
    setFormError(null);
    if (hasErrors(errors)) {
      return;
    }
    setSubmitting(true);
    try {
      const user = await register({ displayName: displayName.trim(), email: email.trim(), password });
      setUser(user);
      router.replace(DEFAULT_AFTER_LOGIN);
    } catch (error) {
      if (isApiErrorWithCode(error, "email_taken")) {
        setFieldErrors({ email: error.message });
      } else {
        const mapped = mapServerError(error, SERVER_FIELDS);
        setFieldErrors(mapped.fieldErrors);
        setFormError(mapped.formError);
      }
      setSubmitting(false);
    }
  }

  return (
    <form noValidate onSubmit={handleSubmit} className="flex flex-col gap-4" aria-label="Create account">
      <FormError message={formError} />
      <TextField
        label="Display name"
        name="displayName"
        autoComplete="nickname"
        required
        maxLength={DISPLAY_NAME_MAX + 20}
        value={displayName}
        onChange={(e) => setDisplayName(e.target.value)}
        error={fieldErrors.displayName}
      />
      <TextField
        label="Email"
        type="email"
        name="email"
        autoComplete="email"
        required
        maxLength={EMAIL_MAX + 20}
        value={email}
        onChange={(e) => setEmail(e.target.value)}
        error={fieldErrors.email}
      />
      <PasswordField
        label="Password"
        name="password"
        autoComplete="new-password"
        required
        maxLength={PASSWORD_MAX}
        hint="8-128 characters, with at least one letter and one digit."
        value={password}
        onChange={(e) => setPassword(e.target.value)}
        error={fieldErrors.password}
      />
      <PasswordField
        label="Confirm password"
        name="confirmPassword"
        autoComplete="new-password"
        required
        maxLength={PASSWORD_MAX}
        value={confirmPassword}
        onChange={(e) => setConfirmPassword(e.target.value)}
        error={fieldErrors.confirmPassword}
      />
      <Button type="submit" isLoading={submitting}>
        {submitting ? "Creating account…" : "Create account"}
      </Button>
      <p className="text-center text-sm text-slate-700">
        Already have an account?{" "}
        <Link href="/login" className="font-medium text-blue-700 underline hover:text-blue-900">
          Log in
        </Link>
      </p>
    </form>
  );
}
