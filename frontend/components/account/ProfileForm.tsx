"use client";

import { useState, type FormEvent } from "react";
import { updateProfile } from "@/lib/api/account";
import { useAuth } from "@/lib/auth/AuthContext";
import { mapServerError } from "@/lib/forms/serverErrors";
import { DISPLAY_NAME_MAX, validateDisplayName } from "@/lib/validation/rules";
import { Button } from "@/components/ui/Button";
import { FormError } from "@/components/ui/FormError";
import { SuccessNotice } from "@/components/ui/SuccessNotice";
import { TextField } from "@/components/ui/TextField";

const FIELDS = ["displayName"] as const;

/** Edits the display name and updates the auth context (so the header changes without a reload). */
export function ProfileForm() {
  const { user, setUser } = useAuth();
  const [displayName, setDisplayName] = useState(user?.displayName ?? "");
  const [fieldError, setFieldError] = useState<string | undefined>();
  const [formError, setFormError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setNotice(null);
    setFormError(null);
    const error = validateDisplayName(displayName);
    setFieldError(error ?? undefined);
    if (error) {
      return;
    }
    setSaving(true);
    try {
      const updated = await updateProfile({ displayName: displayName.trim() });
      setUser(updated);
      setDisplayName(updated.displayName);
      setNotice("Display name updated.");
    } catch (err) {
      const mapped = mapServerError(err, FIELDS);
      setFieldError(mapped.fieldErrors.displayName);
      setFormError(mapped.formError);
    } finally {
      setSaving(false);
    }
  }

  return (
    <form noValidate onSubmit={handleSubmit} className="flex flex-col gap-4" aria-labelledby="profile-heading">
      <h2 id="profile-heading" className="text-lg font-semibold text-slate-900">
        Profile
      </h2>
      {user ? <p className="text-sm text-slate-700">Email: {user.email}</p> : null}
      <FormError message={formError} />
      <TextField
        label="Display name"
        name="displayName"
        autoComplete="nickname"
        required
        maxLength={DISPLAY_NAME_MAX + 20}
        value={displayName}
        onChange={(e) => setDisplayName(e.target.value)}
        error={fieldError}
      />
      <SuccessNotice message={notice} />
      <div>
        <Button type="submit" isLoading={saving}>
          {saving ? "Saving…" : "Save display name"}
        </Button>
      </div>
    </form>
  );
}
