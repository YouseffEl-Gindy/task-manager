import type { TaskItemStatus } from "@/types/api";
import { STATUS_LABELS } from "@/lib/tasks/labels";

const STYLES: Record<TaskItemStatus, string> = {
  todo: "bg-slate-100 text-slate-800 ring-slate-300",
  inProgress: "bg-blue-50 text-blue-800 ring-blue-300",
  done: "bg-green-50 text-green-800 ring-green-300",
};

export function StatusBadge({ status }: { status: TaskItemStatus }) {
  return (
    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${STYLES[status]}`}>
      <span className="sr-only">Status: </span>
      {STATUS_LABELS[status]}
    </span>
  );
}
