export const DEFAULT_AFTER_LOGIN = "/tasks";

/**
 * SPEC 8.1: a returnUrl is only accepted if it is a relative path that starts
 * with "/" and not "//". Backslashes are also rejected after the leading "/",
 * because browsers treat "/\evil.com" like "//evil.com". Control characters are
 * rejected too. Anything else falls back to `fallback`.
 */
export function safeReturnUrl(value: string | null | undefined, fallback: string = DEFAULT_AFTER_LOGIN): string {
  if (!value || !value.startsWith("/")) {
    return fallback;
  }
  const second = value.charAt(1);
  if (second === "/" || second === "\\") {
    return fallback;
  }
  if (/[\u0000-\u001f\u007f]/.test(value)) {
    return fallback;
  }
  return value;
}

/** `/login?returnUrl=<path>` for a protected path. */
export function loginHref(returnPath?: string): string {
  if (!returnPath || safeReturnUrl(returnPath, "") === "") {
    return "/login";
  }
  return `/login?returnUrl=${encodeURIComponent(returnPath)}`;
}
