import { describe, expect, it } from "vitest";
import {
  collectErrors,
  hasErrors,
  validateDescription,
  validateDisplayName,
  validateEmail,
  validateExistingPassword,
  validateLoginEmail,
  validateNewPassword,
  validatePasswordConfirmation,
  validateTitle,
} from "@/lib/validation/rules";

describe("validation rules (mirror API.md)", () => {
  it("display name: 1-50 chars after trim", () => {
    expect(validateDisplayName("  ")).not.toBeNull();
    expect(validateDisplayName(" Ada ")).toBeNull();
    expect(validateDisplayName("a".repeat(50))).toBeNull();
    expect(validateDisplayName("a".repeat(51))).not.toBeNull();
  });

  it("email: pattern and max 254 after trim", () => {
    expect(validateEmail(" ada@example.com ")).toBeNull();
    expect(validateEmail("")).toBe("Email is required.");
    expect(validateEmail("ada@example")).not.toBeNull();
    expect(validateEmail("ada example@x.com")).not.toBeNull();
    expect(validateEmail(`${"a".repeat(245)}@example.com`)).not.toBeNull();
  });

  it("new password: 8-128 chars with a letter and a digit, never trimmed", () => {
    expect(validateNewPassword("correcthorse1")).toBeNull();
    expect(validateNewPassword("short1")).not.toBeNull();
    expect(validateNewPassword("allletters")).not.toBeNull();
    expect(validateNewPassword("12345678")).not.toBeNull();
    expect(validateNewPassword(`a1${"x".repeat(127)}`)).not.toBeNull();
    expect(validateNewPassword("  abc123  ")).toBeNull();
    expect(validateNewPassword("       1")).not.toBeNull();
  });

  it("login fields: required only", () => {
    expect(validateLoginEmail(" ")).not.toBeNull();
    expect(validateLoginEmail("whatever")).toBeNull();
    expect(validateExistingPassword("")).not.toBeNull();
    expect(validateExistingPassword("x")).toBeNull();
  });

  it("password confirmation must match", () => {
    expect(validatePasswordConfirmation("abc12345", "")).not.toBeNull();
    expect(validatePasswordConfirmation("abc12345", "abc12346")).toBe("Passwords do not match.");
    expect(validatePasswordConfirmation("abc12345", "abc12345")).toBeNull();
  });

  it("title 1-200 and description max 2000 after trim", () => {
    expect(validateTitle("   ")).toBe("Title is required.");
    expect(validateTitle("a".repeat(200))).toBeNull();
    expect(validateTitle("a".repeat(201))).not.toBeNull();
    expect(validateDescription("")).toBeNull();
    expect(validateDescription(` ${"a".repeat(2000)} `)).toBeNull();
    expect(validateDescription("a".repeat(2001))).not.toBeNull();
  });

  it("collectErrors keeps only messages", () => {
    const errors = collectErrors({ a: null, b: "bad" });
    expect(errors).toEqual({ b: "bad" });
    expect(hasErrors(errors)).toBe(true);
    expect(hasErrors(collectErrors({ a: null }))).toBe(false);
  });
});
