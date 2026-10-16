"use client";

import { useState } from "react";
import { TASK_ITEM_STATUSES, type TaskDto, type TaskItemStatus } from "@/types/api";
import { getErrorMessage } from "@/lib/api/client";
import { updateTask } from "@/lib/api/tasks";
import { STATUS_LABELS } from "@/lib/tasks/labels";
import { Select } from "@/components/ui/Select";

const OPTIONS = TASK_ITEM_STATUSES.map((status) => ({ value: status, label: STATUS_LABELS[status] }));

export interface TaskStatusSelectProps {
  task: TaskDto;
  onUpdated: (task: TaskDto) => void;
}

/**
 * Quick status change from the list. Uses the full-replace PUT and re-sends every
 * other field unchanged, including a past dueDate (allowed because it doesn't change).
 */
export function TaskStatusSelect({ task, onUpdated }: TaskStatusSelectProps) {
  const [pending, setPending] = useState<TaskItemStatus | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function handleChange(status: TaskItemStatus) {
    if (status === task.status) {
      return;
    }
    setPending(status);
    setError(null);
    try {
      const updated = await updateTask(task.id, {
        title: task.title,
        description: task.description,
        status,
        priority: task.priority,
        dueDate: task.dueDate,
      });
      onUpdated(updated);
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setPending(null);
    }
  }

  return (
    <div className="flex flex-col gap-1">
      <Select
        label={`Status for ${task.title}`}
        hideLabel
        options={OPTIONS}
        value={pending ?? task.status}
        onValueChange={handleChange}
        disabled={pending !== null}
        aria-busy={pending !== null || undefined}
        className="w-36"
      />
      {error ? (
        <p role="alert" className="max-w-xs text-xs text-red-700">
          {error}
        </p>
      ) : null}
    </div>
  );
}
