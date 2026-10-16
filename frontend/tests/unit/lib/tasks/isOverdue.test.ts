import { describe, expect, it } from "vitest";
import { isOverdue } from "@/lib/tasks/isOverdue";

const NOW = new Date("2026-10-11T12:00:00Z");

describe("isOverdue (AC-LIST-9)", () => {
  it("is true when the due date has passed and the task is not done", () => {
    expect(isOverdue({ dueDate: "2026-10-11T11:59:59Z", status: "todo" }, NOW)).toBe(true);
    expect(isOverdue({ dueDate: "2026-10-01T00:00:00Z", status: "inProgress" }, NOW)).toBe(true);
  });

  it("is false for a done task even when the due date has passed", () => {
    expect(isOverdue({ dueDate: "2026-10-01T00:00:00Z", status: "done" }, NOW)).toBe(false);
  });

  it("is false for a future, equal, or missing due date", () => {
    expect(isOverdue({ dueDate: "2026-10-11T12:00:01Z", status: "todo" }, NOW)).toBe(false);
    expect(isOverdue({ dueDate: "2026-10-11T12:00:00Z", status: "todo" }, NOW)).toBe(false);
    expect(isOverdue({ dueDate: null, status: "todo" }, NOW)).toBe(false);
  });
});
