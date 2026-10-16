import Link from "next/link";
import type { TaskDto } from "@/types/api";
import { DueDateLabel } from "./DueDateLabel";
import { PriorityBadge } from "./PriorityBadge";
import { StatusBadge } from "./StatusBadge";
import { TaskStatusSelect } from "./TaskStatusSelect";

export interface TaskListItemProps {
  task: TaskDto;
  onTaskUpdated: (task: TaskDto) => void;
}

export function TaskListItem({ task, onTaskUpdated }: TaskListItemProps) {
  return (
    <li className="flex flex-col gap-3 rounded-lg border border-slate-200 bg-white p-4 shadow-sm sm:flex-row sm:items-start sm:justify-between">
      <div className="min-w-0 flex-1">
        <Link
          href={`/tasks/${encodeURIComponent(task.id)}`}
          className="break-words text-base font-semibold text-slate-900 underline-offset-2 hover:underline focus-visible:outline-2 focus-visible:outline-blue-700"
        >
          {task.title}
        </Link>
        {task.description ? (
          <p className="mt-1 line-clamp-2 break-words text-sm text-slate-600">{task.description}</p>
        ) : null}
        <div className="mt-2 flex flex-wrap items-center gap-2">
          <StatusBadge status={task.status} />
          <PriorityBadge priority={task.priority} />
          <DueDateLabel task={task} />
        </div>
      </div>
      <TaskStatusSelect task={task} onUpdated={onTaskUpdated} />
    </li>
  );
}
