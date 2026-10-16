import type {
  CreateTaskRequest,
  TaskDto,
  TaskListQuery,
  TaskListResponse,
  UpdateTaskRequest,
} from "@/types/api";
import { toSearchParams } from "@/lib/tasks/queryParams";
import { requestJson, requestVoid } from "./client";

function taskPath(id: string): string {
  return `/tasks/${encodeURIComponent(id)}`;
}

/** GET /tasks -> 200 TaskDto[] (plain array, no pagination). */
export function listTasks(query: TaskListQuery = {}, signal?: AbortSignal): Promise<TaskListResponse> {
  return requestJson<TaskListResponse>("/tasks", { query: toSearchParams(query), signal });
}

/** GET /tasks/{id} -> 200 TaskDto, or 404 not_found. */
export function getTask(id: string, signal?: AbortSignal): Promise<TaskDto> {
  return requestJson<TaskDto>(taskPath(id), { signal });
}

/** POST /tasks -> 201 TaskDto. */
export function createTask(request: CreateTaskRequest): Promise<TaskDto> {
  return requestJson<TaskDto>("/tasks", { method: "POST", body: request });
}

/** PUT /tasks/{id} -> 200 TaskDto. Full replace: every key must be present. */
export function updateTask(id: string, request: UpdateTaskRequest): Promise<TaskDto> {
  return requestJson<TaskDto>(taskPath(id), { method: "PUT", body: request });
}

/** DELETE /tasks/{id} -> 204. */
export function deleteTask(id: string): Promise<void> {
  return requestVoid(taskPath(id), { method: "DELETE" });
}
