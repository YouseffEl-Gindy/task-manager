import { describe, expect, it } from "vitest";
import {
  dateToLocalInputValue,
  formatDateTime,
  isInFuture,
  isSameInstant,
  localInputValueToUtcIso,
  localTimeZoneName,
  parseLocalInputValue,
  toUtcIsoString,
  utcIsoToLocalInputValue,
} from "@/lib/tasks/dateTime";

// vitest.config.mts pins TZ=Europe/Berlin (UTC+2 in October before the DST switch, UTC+1 after).

describe("time zone setup", () => {
  it("runs in Europe/Berlin", () => {
    expect(localTimeZoneName()).toBe("Europe/Berlin");
  });
});

describe("localInputValueToUtcIso (AC-DUE-8)", () => {
  it("converts local wall time to a UTC Z string in whole seconds", () => {
    expect(localInputValueToUtcIso("2026-10-12T17:00")).toBe("2026-10-12T15:00:00Z");
    expect(localInputValueToUtcIso("2026-12-01T09:30:15")).toBe("2026-12-01T08:30:15Z");
  });

  it("drops milliseconds", () => {
    expect(localInputValueToUtcIso("2026-10-12T17:00:00.789")).toBe("2026-10-12T15:00:00Z");
  });

  it("returns null for empty or invalid values", () => {
    expect(localInputValueToUtcIso("")).toBeNull();
    expect(localInputValueToUtcIso("2026-10-12")).toBeNull();
    expect(localInputValueToUtcIso("2026-02-31T10:00")).toBeNull();
    expect(localInputValueToUtcIso("2026-13-01T10:00")).toBeNull();
    expect(localInputValueToUtcIso("not a date")).toBeNull();
  });
});

describe("utcIsoToLocalInputValue", () => {
  it("shows UTC values in local time", () => {
    expect(utcIsoToLocalInputValue("2026-10-12T15:00:00Z")).toBe("2026-10-12T17:00");
    expect(utcIsoToLocalInputValue("2026-12-01T08:30:15Z")).toBe("2026-12-01T09:30:15");
  });

  it("returns an empty string for null or garbage", () => {
    expect(utcIsoToLocalInputValue(null)).toBe("");
    expect(utcIsoToLocalInputValue("nope")).toBe("");
  });

  it("round-trips", () => {
    const iso = "2026-10-12T15:00:00Z";
    expect(localInputValueToUtcIso(utcIsoToLocalInputValue(iso))).toBe(iso);
  });
});

describe("helpers", () => {
  it("dateToLocalInputValue omits zero seconds", () => {
    expect(dateToLocalInputValue(new Date(2026, 9, 12, 7, 5, 0))).toBe("2026-10-12T07:05");
    expect(dateToLocalInputValue(new Date(2026, 9, 12, 7, 5, 9))).toBe("2026-10-12T07:05:09");
  });

  it("parseLocalInputValue builds a local Date", () => {
    const date = parseLocalInputValue("2026-10-12T17:00");
    expect(date?.getHours()).toBe(17);
    expect(date && toUtcIsoString(date)).toBe("2026-10-12T15:00:00Z");
  });

  it("isSameInstant compares instants regardless of offset", () => {
    expect(isSameInstant("2026-10-12T17:00:00+02:00", "2026-10-12T15:00:00Z")).toBe(true);
    expect(isSameInstant("2026-10-12T15:00:01Z", "2026-10-12T15:00:00Z")).toBe(false);
    expect(isSameInstant(null, null)).toBe(true);
    expect(isSameInstant(null, "2026-10-12T15:00:00Z")).toBe(false);
  });

  it("isInFuture is strict", () => {
    const now = new Date("2026-10-11T12:00:00Z");
    expect(isInFuture("2026-10-11T12:00:01Z", now)).toBe(true);
    expect(isInFuture("2026-10-11T12:00:00Z", now)).toBe(false);
    expect(isInFuture("2026-10-11T11:00:00Z", now)).toBe(false);
  });

  it("formatDateTime renders local date and time", () => {
    const text = formatDateTime("2026-10-12T15:00:00Z", "en-GB");
    expect(text).toContain("12 Oct 2026");
    expect(text).toContain("17:00");
  });
});
