"use client";

import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import { deleteAccount } from "@/lib/api/account";
import { useAuth } from "@/lib/auth/AuthContext";
import { mapServerError } from "@/lib/forms/serverErrors";
import { PASSWORD_MAX, validateExistingPassword } from "@/lib/validation/rules";
import { Button } from "@/components/ui/Button";
import { ConfirmDialog } from "@/components/ui/ConfirmDialog";
import { FormError } from "@/components/ui/FormError";
import { PasswordField } from "@/components/ui/PasswordField";

const FIELDS = ["password"] as const;

/** Permanent account deletion: password, then a confirmation dialog, then POST /account/delete and /login. */
export function DeleteAccountSection() {
  const router = useRouter();
  const { markSignedOut } = useAuth();
  const [password, setPassword] = useState("");
  const [fieldError, setFieldError] = useState<string | undefined>();
  const [formError, setFormError] = useState<string | null>(null);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [deleting, setDeleting] = useState(false);

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFormError(null);
    const error = validateExistingPassword(password);
    setFieldError(error ?? undefined);
    if (!error) {
      setConfirmOpen(true);
    }
  }

  async function handleConfirm() {
    setDeleting(true);
    try {
      await deleteAccount({ password });
      setConfirmOpen(false);
      markSignedOut();
      router.replace("/login");
    } catch (err) {
      const mapped = mapServerError(err, FIELDS);
      setFieldError(mapped.fieldErrors.password);
      setFormError(mapped.formError);
      setConfirmOpen(false);
      setDeleting(false);
    }
  }

  return (
    <section aria-labelledby="delete-heading" className="flex flex-col gap-4">
      <h2 id="delete-heading" className="text-lg font-semibold text-red-800">
        Delete account
      </h2>
      <p className="text-sm text-slate-700">
        This permanently deletes your account and all of your tasks. It cannot be undone.
      </p>
      <form noValidate onSubmit={handleSubmit} className="flex flex-col gap-4" aria-label="Delete account">
        <FormError message={formError} />
        <PasswordField
          label="Password"
          name="deletePassword"
          autoComplete="current-password"
          required
          maxLength={PASSWORD_MAX}
          hint="Enter your password to confirm it's you."
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          error={fieldError}
        />
        <div>
          <Button type="submit" variant="danger">
            Delete my account
          </Button>
        </div>
      </form>
      <ConfirmDialog
        open={confirmOpen}
        title="Delete your account?"
        confirmLabel="Delete account"
        isConfirming={deleting}
        onConfirm={handleConfirm}
        onCancel={() => setConfirmOpen(false)}
      >
        Your account and all of your tasks will be permanently deleted.
      </ConfirmDialog>
    </section>
  );
}
