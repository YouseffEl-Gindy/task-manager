import type { TaskDto } from "@/types/api";
import { formatDateTime } from "@/lib/tasks/dateTime";
import { isOverdue } from "@/lib/tasks/isOverdue";

export interface DueDateLabelProps {
  task: Pick<TaskDto, "dueDate" | "status">;
  /** Injectable for tests. */
  now?: Date;
}

/** Due date and time in the user's local time, plus "Overdue" when due < now and status != done. */
export function DueDateLabel({ task, now }: DueDateLabelProps) {
  if (task.dueDate === null) {
    return <span className="text-xs text-slate-500">No due date</span>;
  }
  const overdue = isOverdue(task, now);
  return (
    <span className="inline-flex flex-wrap items-center gap-2 text-xs text-slate-700">
      <span>
        Due <time dateTime={task.dueDate}>{formatDateTime(task.dueDate)}</time>
      </span>
      {overdue ? (
        <span className="rounded-full bg-red-700 px-2 py-0.5 font-semibold text-white">Overdue</span>
      ) : null}
    </span>
  );
}
