import { describe, expect, it } from "vitest";
import { loginHref, safeReturnUrl } from "@/lib/auth/safeReturnUrl";

describe("safeReturnUrl (AC-AUTH-12)", () => {
  it("accepts relative paths", () => {
    expect(safeReturnUrl("/tasks")).toBe("/tasks");
    expect(safeReturnUrl("/tasks?status=done&sortBy=title")).toBe("/tasks?status=done&sortBy=title");
    expect(safeReturnUrl("/tasks/3f2b8c1e-9a4d-4c7e-8f61-2d5a7b9c0e14")).toBe("/tasks/3f2b8c1e-9a4d-4c7e-8f61-2d5a7b9c0e14");
  });

  it("rejects protocol-relative and absolute URLs", () => {
    expect(safeReturnUrl("//evil.com")).toBe("/tasks");
    expect(safeReturnUrl("/\\evil.com")).toBe("/tasks");
    expect(safeReturnUrl("https://evil.com")).toBe("/tasks");
    expect(safeReturnUrl("javascript:alert(1)")).toBe("/tasks");
    expect(safeReturnUrl("tasks")).toBe("/tasks");
  });

  it("rejects control characters", () => {
    expect(safeReturnUrl("/\t/evil.com")).toBe("/tasks");
    expect(safeReturnUrl("/tasks\n")).toBe("/tasks");
  });

  it("falls back for missing values and supports a custom fallback", () => {
    expect(safeReturnUrl(null)).toBe("/tasks");
    expect(safeReturnUrl(undefined)).toBe("/tasks");
    expect(safeReturnUrl("")).toBe("/tasks");
    expect(safeReturnUrl("//x", "/account")).toBe("/account");
  });
});

describe("loginHref", () => {
  it("encodes the return path", () => {
    expect(loginHref("/tasks")).toBe("/login?returnUrl=%2Ftasks");
    expect(loginHref("/tasks?status=done")).toBe("/login?returnUrl=%2Ftasks%3Fstatus%3Ddone");
  });

  it("omits unsafe or empty paths", () => {
    expect(loginHref()).toBe("/login");
    expect(loginHref("//evil.com")).toBe("/login");
  });
});
