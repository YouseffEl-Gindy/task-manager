/**
 * Due date handling (FR-T9). The API speaks UTC ISO 8601 with `Z`; the UI shows
 * and edits local time through `<input type="datetime-local">`, whose value is
 * `YYYY-MM-DDTHH:mm` or `YYYY-MM-DDTHH:mm:ss` with no offset (local wall time).
 */

const LOCAL_INPUT_PATTERN = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2})(?:\.\d{1,3})?)?$/;

function pad(value: number, length = 2): string {
  return String(value).padStart(length, "0");
}

/** A Date as a datetime-local value in the browser's time zone. Seconds are included only when non-zero. */
export function dateToLocalInputValue(date: Date): string {
  const base = `${pad(date.getFullYear(), 4)}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
  return date.getSeconds() === 0 ? base : `${base}:${pad(date.getSeconds())}`;
}

/** UTC ISO string from the API -> datetime-local value. Returns "" for null or an unparseable value. */
export function utcIsoToLocalInputValue(iso: string | null): string {
  if (iso === null) {
    return "";
  }
  const time = Date.parse(iso);
  return Number.isNaN(time) ? "" : dateToLocalInputValue(new Date(time));
}

/** datetime-local value -> Date in the browser's time zone, or null if invalid. */
export function parseLocalInputValue(value: string): Date | null {
  const match = LOCAL_INPUT_PATTERN.exec(value.trim());
  if (!match) {
    return null;
  }
  const [, y, mo, d, h, mi, s] = match;
  const year = Number(y);
  const month = Number(mo);
  const day = Number(d);
  const hours = Number(h);
  const minutes = Number(mi);
  const seconds = s === undefined ? 0 : Number(s);
  if (month < 1 || month > 12 || day < 1 || day > 31 || hours > 23 || minutes > 59 || seconds > 59) {
    return null;
  }
  const date = new Date(year, month - 1, day, hours, minutes, seconds, 0);
  // Reject rollovers such as 2026-02-31.
  if (date.getFullYear() !== year || date.getMonth() !== month - 1 || date.getDate() !== day) {
    return null;
  }
  return date;
}

/** A Date as a UTC ISO string with `Z`, in whole seconds (the precision the API stores). */
export function toUtcIsoString(date: Date): string {
  return date.toISOString().replace(/\.\d{3}Z$/, "Z");
}

/** datetime-local value -> UTC ISO string with `Z`, or null if empty or invalid. */
export function localInputValueToUtcIso(value: string): string | null {
  const date = parseLocalInputValue(value);
  return date === null ? null : toUtcIsoString(date);
}

/** True if both values denote the same instant (null equals null). */
export function isSameInstant(a: string | null, b: string | null): boolean {
  if (a === null || b === null) {
    return a === b;
  }
  return Date.parse(a) === Date.parse(b);
}

/** True if the ISO instant is strictly later than `now`. */
export function isInFuture(iso: string, now: Date = new Date()): boolean {
  const time = Date.parse(iso);
  return !Number.isNaN(time) && time > now.getTime();
}

/** Formats an ISO instant in the user's locale and time zone (date and time). */
export function formatDateTime(iso: string, locale?: string): string {
  const time = Date.parse(iso);
  if (Number.isNaN(time)) {
    return iso;
  }
  return new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeStyle: "short" }).format(new Date(time));
}

/** The browser's IANA time zone name, e.g. "Europe/Berlin". */
export function localTimeZoneName(): string {
  return Intl.DateTimeFormat().resolvedOptions().timeZone;
}
