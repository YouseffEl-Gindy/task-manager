/**
 * Client-side validation that mirrors the API rules (API.md 4 and 5).
 * It only gives quicker feedback; the server stays authoritative.
 * Each function returns an error message, or null when the value is valid.
 */

export const DISPLAY_NAME_MAX = 50;
export const EMAIL_MAX = 254;
export const PASSWORD_MIN = 8;
export const PASSWORD_MAX = 128;
export const TITLE_MAX = 200;
export const DESCRIPTION_MAX = 2000;

const EMAIL_PATTERN = /^[^@\s]+@[^@\s]+\.[^@\s]+$/;

export function validateDisplayName(value: string): string | null {
  const trimmed = value.trim();
  if (trimmed === "") return "Display name is required.";
  if (trimmed.length > DISPLAY_NAME_MAX) return `Display name must be at most ${DISPLAY_NAME_MAX} characters.`;
  return null;
}

export function validateEmail(value: string): string | null {
  const trimmed = value.trim();
  if (trimmed === "") return "Email is required.";
  if (trimmed.length > EMAIL_MAX) return `Email must be at most ${EMAIL_MAX} characters.`;
  if (!EMAIL_PATTERN.test(trimmed)) return "Enter a valid email address.";
  return null;
}

/** Password rules for register and change password. Passwords are never trimmed. */
export function validateNewPassword(value: string): string | null {
  if (value === "") return "Password is required.";
  if (value.length < PASSWORD_MIN) return `Password must be at least ${PASSWORD_MIN} characters.`;
  if (value.length > PASSWORD_MAX) return `Password must be at most ${PASSWORD_MAX} characters.`;
  if (!/[A-Za-z]/.test(value) || !/[0-9]/.test(value)) return "Password must contain at least one letter and one digit.";
  return null;
}

/** Login / current password: required, max length only. */
export function validateExistingPassword(value: string): string | null {
  if (value === "") return "Password is required.";
  if (value.length > PASSWORD_MAX) return `Password must be at most ${PASSWORD_MAX} characters.`;
  return null;
}

export function validateLoginEmail(value: string): string | null {
  const trimmed = value.trim();
  if (trimmed === "") return "Email is required.";
  if (trimmed.length > EMAIL_MAX) return `Email must be at most ${EMAIL_MAX} characters.`;
  return null;
}

export function validatePasswordConfirmation(password: string, confirmation: string): string | null {
  if (confirmation === "") return "Please confirm the password.";
  if (password !== confirmation) return "Passwords do not match.";
  return null;
}

export function validateTitle(value: string): string | null {
  const trimmed = value.trim();
  if (trimmed === "") return "Title is required.";
  if (trimmed.length > TITLE_MAX) return `Title must be at most ${TITLE_MAX} characters.`;
  return null;
}

export function validateDescription(value: string): string | null {
  if (value.trim().length > DESCRIPTION_MAX) return `Description must be at most ${DESCRIPTION_MAX} characters.`;
  return null;
}

/** Removes null entries; an empty result means the form is valid. */
export function collectErrors<K extends string>(checks: Record<K, string | null>): Partial<Record<K, string>> {
  const errors: Partial<Record<K, string>> = {};
  for (const key of Object.keys(checks) as K[]) {
    const message = checks[key];
    if (message !== null) {
      errors[key] = message;
    }
  }
  return errors;
}

export function hasErrors(errors: Partial<Record<string, string>>): boolean {
  return Object.keys(errors).length > 0;
}
