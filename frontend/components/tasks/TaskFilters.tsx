"use client";

import {
  TASK_ITEM_PRIORITIES,
  TASK_ITEM_STATUSES,
  type TaskItemPriority,
  type TaskItemStatus,
} from "@/types/api";
import { PRIORITY_LABELS, STATUS_LABELS } from "@/lib/tasks/labels";
import { Select } from "@/components/ui/Select";

const ALL = "all";
type All = typeof ALL;

const STATUS_OPTIONS = [
  { value: ALL, label: "All statuses" },
  ...TASK_ITEM_STATUSES.map((s) => ({ value: s, label: STATUS_LABELS[s] })),
] as const satisfies readonly { value: TaskItemStatus | All; label: string }[];

const PRIORITY_OPTIONS = [
  { value: ALL, label: "All priorities" },
  ...TASK_ITEM_PRIORITIES.map((p) => ({ value: p, label: PRIORITY_LABELS[p] })),
] as const satisfies readonly { value: TaskItemPriority | All; label: string }[];

export interface TaskFiltersProps {
  status?: TaskItemStatus;
  priority?: TaskItemPriority;
  onStatusChange: (status: TaskItemStatus | undefined) => void;
  onPriorityChange: (priority: TaskItemPriority | undefined) => void;
}

export function TaskFilters({ status, priority, onStatusChange, onPriorityChange }: TaskFiltersProps) {
  return (
    <>
      <Select<TaskItemStatus | All>
        label="Status"
        options={STATUS_OPTIONS}
        value={status ?? ALL}
        onValueChange={(value) => onStatusChange(value === ALL ? undefined : value)}
      />
      <Select<TaskItemPriority | All>
        label="Priority"
        options={PRIORITY_OPTIONS}
        value={priority ?? ALL}
        onValueChange={(value) => onPriorityChange(value === ALL ? undefined : value)}
      />
    </>
  );
}
