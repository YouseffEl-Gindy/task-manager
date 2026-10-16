import { describe, expect, it } from "vitest";
import {
  hasActiveFilters,
  parseTaskListQuery,
  tasksHref,
  toSearchParams,
} from "@/lib/tasks/queryParams";

describe("parseTaskListQuery", () => {
  it("reads every valid key", () => {
    const query = parseTaskListQuery(
      new URLSearchParams("status=inProgress&priority=high&search=report&sortBy=dueDate&sortDir=asc"),
    );
    expect(query).toEqual({ status: "inProgress", priority: "high", search: "report", sortBy: "dueDate", sortDir: "asc" });
  });

  it("drops invalid values so they are never sent to the API", () => {
    const query = parseTaskListQuery(
      new URLSearchParams("status=1&priority=urgent&sortBy=name&sortDir=up&search=%20%20"),
    );
    expect(query).toEqual({});
  });

  it("is case-sensitive for enum names (camelCase only)", () => {
    expect(parseTaskListQuery(new URLSearchParams("status=InProgress&priority=HIGH"))).toEqual({});
  });

  it("trims search and drops one longer than 100 characters", () => {
    expect(parseTaskListQuery(new URLSearchParams("search=%20milk%20")).search).toBe("milk");
    expect(parseTaskListQuery(new URLSearchParams(`search=${"a".repeat(100)}`)).search).toHaveLength(100);
    expect(parseTaskListQuery(new URLSearchParams(`search=${"a".repeat(101)}`)).search).toBeUndefined();
  });

  it("keeps literal special characters in search", () => {
    expect(parseTaskListQuery(new URLSearchParams({ search: "100%_\\[" })).search).toBe("100%_\\[");
  });
});

describe("toSearchParams / tasksHref", () => {
  it("serializes only set keys in a stable order", () => {
    expect(toSearchParams({ sortDir: "asc", status: "done" }).toString()).toBe("status=done&sortDir=asc");
    expect(toSearchParams({}).toString()).toBe("");
  });

  it("round-trips through the URL", () => {
    const query = { status: "todo", priority: "low", search: "a&b=c", sortBy: "title", sortDir: "desc" } as const;
    expect(parseTaskListQuery(toSearchParams(query))).toEqual(query);
  });

  it("builds /tasks links", () => {
    expect(tasksHref({})).toBe("/tasks");
    expect(tasksHref({ priority: "high", search: undefined })).toBe("/tasks?priority=high");
  });
});

describe("hasActiveFilters", () => {
  it("counts filters and search but not sorting", () => {
    expect(hasActiveFilters({ sortBy: "title", sortDir: "asc" })).toBe(false);
    expect(hasActiveFilters({ status: "done" })).toBe(true);
    expect(hasActiveFilters({ search: "x" })).toBe(true);
  });
});
