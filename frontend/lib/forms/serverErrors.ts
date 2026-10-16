import { ApiResponseError, getErrorMessage } from "@/lib/api/client";

export interface MappedServerError<K extends string> {
  /** First server message per known field (shown next to the input). */
  fieldErrors: Partial<Record<K, string>>;
  /** Message for the form as a whole, or null when every error belongs to a field. */
  formError: string | null;
}

/**
 * Splits a thrown error into field errors (for `validation_failed` keys this form
 * knows about) and a form-level message (everything else, including the `body` key).
 */
export function mapServerError<K extends string>(error: unknown, fields: readonly K[]): MappedServerError<K> {
  if (!(error instanceof ApiResponseError) || error.code !== "validation_failed" || error.fieldErrors === null) {
    return { fieldErrors: {}, formError: getErrorMessage(error) };
  }
  const fieldErrors: Partial<Record<K, string>> = {};
  const otherMessages: string[] = [];
  for (const [key, messages] of Object.entries(error.fieldErrors)) {
    const first = messages[0];
    if (first === undefined) {
      continue;
    }
    if ((fields as readonly string[]).includes(key)) {
      fieldErrors[key as K] = first;
    } else {
      otherMessages.push(first);
    }
  }
  const formError =
    otherMessages.length > 0
      ? otherMessages.join(" ")
      : Object.keys(fieldErrors).length === 0
        ? error.message
        : null;
  return { fieldErrors, formError };
}
