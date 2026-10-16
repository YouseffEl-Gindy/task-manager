/**
 * Shared API types for the Task Manager frontend.
 *
 * Source of truth: docs/API.md (section 6). These types match the C# DTOs
 * field for field. Do not change this file without updating API.md and the
 * backend DTOs in the same change.
 *
 * All API calls go to the relative base path API_BASE_PATH on the frontend
 * origin; Next.js rewrites proxy them to the backend.
 */

// ---------- Transport constants ----------

/** Relative base path for every API call (proxied by Next.js rewrites). */
export const API_BASE_PATH = "/api/v1" as const;

/** Header required on every POST/PUT/PATCH/DELETE request (CSRF protection). */
export const CSRF_HEADER_NAME = "X-CSRF-Protection" as const;
export const CSRF_HEADER_VALUE = "1" as const;

// ---------- Primitives ----------

/** GUID string, e.g. "3f2b8c1e-9a4d-4c7e-8f61-2d5a7b9c0e14". */
export type Guid = string;

/**
 * ISO 8601 timestamp.
 * - In responses: always UTC with "Z", e.g. "2026-10-12T15:00:00Z".
 * - In requests (dueDate): must include an offset or "Z"; send UTC "Z" from the UI
 *   (e.g. `new Date(localValue).toISOString()`).
 */
export type IsoDateTimeString = string;

// ---------- Enums ----------

export type TaskItemStatus = "todo" | "inProgress" | "done";
export const TASK_ITEM_STATUSES: readonly TaskItemStatus[] = ["todo", "inProgress", "done"];

export type TaskItemPriority = "low" | "medium" | "high";
export const TASK_ITEM_PRIORITIES: readonly TaskItemPriority[] = ["low", "medium", "high"];

export type TaskSortBy = "createdAt" | "dueDate" | "priority" | "title";
export const TASK_SORT_BY_VALUES: readonly TaskSortBy[] = ["createdAt", "dueDate", "priority", "title"];

export type SortDirection = "asc" | "desc";
export const SORT_DIRECTIONS: readonly SortDirection[] = ["asc", "desc"];

// ---------- Auth ----------

/** POST /auth/register -> 201 UserDto (+ session cookie) */
export interface RegisterRequest {
  displayName: string;
  email: string;
  password: string;
}

/** POST /auth/login -> 200 UserDto (+ session cookie) */
export interface LoginRequest {
  email: string;
  password: string;
}

/** GET /auth/me, register, login, PUT /account/profile */
export interface UserDto {
  id: Guid;
  email: string;
  displayName: string;
  createdAt: IsoDateTimeString;
}

// ---------- Account ----------

/** PUT /account/profile -> 200 UserDto */
export interface UpdateProfileRequest {
  displayName: string;
}

/** POST /account/change-password -> 204 */
export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

/** POST /account/delete -> 204 (session cookie expired) */
export interface DeleteAccountRequest {
  password: string;
}

// ---------- Tasks ----------

export interface TaskDto {
  id: Guid;
  title: string;
  description: string | null;
  status: TaskItemStatus;
  priority: TaskItemPriority;
  /** UTC "Z", whole seconds; null when no due date. */
  dueDate: IsoDateTimeString | null;
  createdAt: IsoDateTimeString;
  updatedAt: IsoDateTimeString;
}

/**
 * POST /tasks -> 201 TaskDto.
 * Omitted status defaults to "todo"; omitted priority to "medium".
 * A non-null dueDate must be in the future.
 */
export interface CreateTaskRequest {
  title: string;
  description?: string | null;
  status?: TaskItemStatus;
  priority?: TaskItemPriority;
  dueDate?: IsoDateTimeString | null;
}

/**
 * PUT /tasks/{id} -> 200 TaskDto. Full replace, last write wins; also used for
 * the quick status change. Every key is required; null clears description/dueDate.
 * A past dueDate is rejected only if it differs from the stored value, so
 * re-sending the stored dueDate is always allowed.
 */
export interface UpdateTaskRequest {
  title: string;
  description: string | null;
  status: TaskItemStatus;
  priority: TaskItemPriority;
  dueDate: IsoDateTimeString | null;
}

/** Query string for GET /tasks -> 200 TaskDto[]. Omitted keys use server defaults. */
export interface TaskListQuery {
  status?: TaskItemStatus;
  priority?: TaskItemPriority;
  /** Max 100 chars; case-insensitive match against title and description. */
  search?: string;
  /** Default "createdAt". */
  sortBy?: TaskSortBy;
  /** Default "desc". */
  sortDir?: SortDirection;
}

/** Response body of GET /tasks. */
export type TaskListResponse = TaskDto[];

// ---------- Errors ----------

export type ApiErrorCode =
  | "validation_failed"
  | "bad_request"
  | "invalid_credentials"
  | "unauthorized"
  | "csrf_failed"
  | "not_found"
  | "email_taken"
  | "rate_limited"
  | "internal_error";

/** Body of every non-2xx response from /api/v1/*. */
export interface ApiError {
  status: number;
  code: ApiErrorCode;
  message: string;
  /** Non-null only when code === "validation_failed". Keys are camelCase field names, or "body". */
  errors: Record<string, string[]> | null;
  traceId: string;
}
