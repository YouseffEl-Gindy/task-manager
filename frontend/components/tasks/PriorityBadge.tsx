import type { TaskItemPriority } from "@/types/api";
import { PRIORITY_LABELS } from "@/lib/tasks/labels";

const STYLES: Record<TaskItemPriority, string> = {
  low: "bg-slate-50 text-slate-700 ring-slate-300",
  medium: "bg-amber-50 text-amber-800 ring-amber-300",
  high: "bg-red-50 text-red-800 ring-red-300",
};

export function PriorityBadge({ priority }: { priority: TaskItemPriority }) {
  return (
    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${STYLES[priority]}`}>
      {PRIORITY_LABELS[priority]}
      <span className="sr-only"> priority</span>
    </span>
  );
}
