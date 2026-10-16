"use client";

import { useState, type FormEvent } from "react";
import { changePassword } from "@/lib/api/account";
import { mapServerError } from "@/lib/forms/serverErrors";
import {
  PASSWORD_MAX,
  collectErrors,
  hasErrors,
  validateExistingPassword,
  validateNewPassword,
  validatePasswordConfirmation,
} from "@/lib/validation/rules";
import { Button } from "@/components/ui/Button";
import { FormError } from "@/components/ui/FormError";
import { PasswordField } from "@/components/ui/PasswordField";
import { SuccessNotice } from "@/components/ui/SuccessNotice";

type Field = "currentPassword" | "newPassword" | "confirmNewPassword";
const SERVER_FIELDS: readonly Field[] = ["currentPassword", "newPassword"];

function validate(current: string, next: string, confirm: string): Partial<Record<Field, string>> {
  const errors = collectErrors<Field>({
    currentPassword: validateExistingPassword(current),
    newPassword: validateNewPassword(next),
    confirmNewPassword: validatePasswordConfirmation(next, confirm),
  });
  if (!errors.newPassword && current !== "" && next === current) {
    errors.newPassword = "The new password must be different from the current password.";
  }
  return errors;
}

export function ChangePasswordForm() {
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmNewPassword, setConfirmNewPassword] = useState("");
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<Field, string>>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setNotice(null);
    setFormError(null);
    const errors = validate(currentPassword, newPassword, confirmNewPassword);
    setFieldErrors(errors);
    if (hasErrors(errors)) {
      return;
    }
    setSaving(true);
    try {
      await changePassword({ currentPassword, newPassword });
      setCurrentPassword("");
      setNewPassword("");
      setConfirmNewPassword("");
      setNotice("Your password has been changed.");
    } catch (err) {
      // A wrong current password is 400 validation_failed keyed `currentPassword` (never a 401).
      const mapped = mapServerError(err, SERVER_FIELDS);
      setFieldErrors(mapped.fieldErrors);
      setFormError(mapped.formError);
    } finally {
      setSaving(false);
    }
  }

  return (
    <form noValidate onSubmit={handleSubmit} className="flex flex-col gap-4" aria-labelledby="password-heading">
      <h2 id="password-heading" className="text-lg font-semibold text-slate-900">
        Change password
      </h2>
      <FormError message={formError} />
      <PasswordField
        label="Current password"
        name="currentPassword"
        autoComplete="current-password"
        required
        maxLength={PASSWORD_MAX}
        value={currentPassword}
        onChange={(e) => setCurrentPassword(e.target.value)}
        error={fieldErrors.currentPassword}
      />
      <PasswordField
        label="New password"
        name="newPassword"
        autoComplete="new-password"
        required
        maxLength={PASSWORD_MAX}
        hint="8-128 characters, with at least one letter and one digit."
        value={newPassword}
        onChange={(e) => setNewPassword(e.target.value)}
        error={fieldErrors.newPassword}
      />
      <PasswordField
        label="Confirm new password"
        name="confirmNewPassword"
        autoComplete="new-password"
        required
        maxLength={PASSWORD_MAX}
        value={confirmNewPassword}
        onChange={(e) => setConfirmNewPassword(e.target.value)}
        error={fieldErrors.confirmNewPassword}
      />
      <SuccessNotice message={notice} />
      <div>
        <Button type="submit" isLoading={saving}>
          {saving ? "Changing password…" : "Change password"}
        </Button>
      </div>
    </form>
  );
}
