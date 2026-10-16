/**
 * URL query <-> TaskListQuery (SPEC 8.3).
 * Keys match the API: status, priority, search, sortBy, sortDir.
 * Invalid values are dropped (the server default applies) and never sent to the API.
 */
import {
  SORT_DIRECTIONS,
  TASK_ITEM_PRIORITIES,
  TASK_ITEM_STATUSES,
  TASK_SORT_BY_VALUES,
  type SortDirection,
  type TaskItemPriority,
  type TaskItemStatus,
  type TaskListQuery,
  type TaskSortBy,
} from "@/types/api";

export const SEARCH_MAX_LENGTH = 100;
export const DEFAULT_SORT_BY: TaskSortBy = "createdAt";
export const DEFAULT_SORT_DIR: SortDirection = "desc";

/** Anything with a `get` like URLSearchParams / ReadonlyURLSearchParams. */
export interface QueryReader {
  get(name: string): string | null;
}

function pick<T extends string>(allowed: readonly T[], value: string | null): T | undefined {
  return value !== null && (allowed as readonly string[]).includes(value) ? (value as T) : undefined;
}

export function parseSearch(value: string | null): string | undefined {
  if (value === null) {
    return undefined;
  }
  const trimmed = value.trim();
  if (trimmed === "" || trimmed.length > SEARCH_MAX_LENGTH) {
    return undefined;
  }
  return trimmed;
}

export function parseTaskListQuery(params: QueryReader): TaskListQuery {
  const query: TaskListQuery = {};
  const status = pick<TaskItemStatus>(TASK_ITEM_STATUSES, params.get("status"));
  const priority = pick<TaskItemPriority>(TASK_ITEM_PRIORITIES, params.get("priority"));
  const search = parseSearch(params.get("search"));
  const sortBy = pick<TaskSortBy>(TASK_SORT_BY_VALUES, params.get("sortBy"));
  const sortDir = pick<SortDirection>(SORT_DIRECTIONS, params.get("sortDir"));
  if (status) query.status = status;
  if (priority) query.priority = priority;
  if (search) query.search = search;
  if (sortBy) query.sortBy = sortBy;
  if (sortDir) query.sortDir = sortDir;
  return query;
}

/** Serializes only the keys that are set, in a stable order. Used for both the page URL and the API call. */
export function toSearchParams(query: TaskListQuery): URLSearchParams {
  const params = new URLSearchParams();
  if (query.status) params.set("status", query.status);
  if (query.priority) params.set("priority", query.priority);
  const search = query.search?.trim();
  if (search) params.set("search", search);
  if (query.sortBy) params.set("sortBy", query.sortBy);
  if (query.sortDir) params.set("sortDir", query.sortDir);
  return params;
}

/** `/tasks` or `/tasks?...` for the given query. */
export function tasksHref(query: TaskListQuery): string {
  const qs = toSearchParams(query).toString();
  return qs ? `/tasks?${qs}` : "/tasks";
}

/** True when any filter or search narrows the list (sorting doesn't count). */
export function hasActiveFilters(query: TaskListQuery): boolean {
  return Boolean(query.status || query.priority || query.search);
}

/** A stable string key for effects that depend on the query. */
export function queryKey(query: TaskListQuery): string {
  return toSearchParams(query).toString();
}
