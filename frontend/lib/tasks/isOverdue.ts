import type { TaskDto } from "@/types/api";

/** FR-T11: overdue = dueDate < now AND status != done. Frontend only. */
export function isOverdue(task: Pick<TaskDto, "dueDate" | "status">, now: Date = new Date()): boolean {
  if (task.dueDate === null || task.status === "done") {
    return false;
  }
  const due = Date.parse(task.dueDate);
  return !Number.isNaN(due) && due < now.getTime();
}
