"use client";

import { TASK_SORT_BY_VALUES, type SortDirection, type TaskSortBy } from "@/types/api";
import { DEFAULT_SORT_BY, DEFAULT_SORT_DIR } from "@/lib/tasks/queryParams";
import { SORT_BY_LABELS, SORT_DIR_LABELS } from "@/lib/tasks/labels";
import { Select } from "@/components/ui/Select";
import { Button } from "@/components/ui/Button";

const SORT_OPTIONS = TASK_SORT_BY_VALUES.map((value) => ({ value, label: SORT_BY_LABELS[value] }));

export interface TaskSortControlProps {
  sortBy?: TaskSortBy;
  sortDir?: SortDirection;
  onSortByChange: (sortBy: TaskSortBy) => void;
  onSortDirChange: (sortDir: SortDirection) => void;
}

export function TaskSortControl({ sortBy, sortDir, onSortByChange, onSortDirChange }: TaskSortControlProps) {
  const effectiveDir = sortDir ?? DEFAULT_SORT_DIR;
  const nextDir: SortDirection = effectiveDir === "asc" ? "desc" : "asc";

  return (
    <div className="flex items-end gap-2">
      <Select<TaskSortBy>
        label="Sort by"
        options={SORT_OPTIONS}
        value={sortBy ?? DEFAULT_SORT_BY}
        onValueChange={onSortByChange}
        className="flex-1"
      />
      <Button
        variant="secondary"
        onClick={() => onSortDirChange(nextDir)}
        aria-label={`Sort direction: ${SORT_DIR_LABELS[effectiveDir]}. Switch to ${SORT_DIR_LABELS[nextDir].toLowerCase()}.`}
        title={`Switch to ${SORT_DIR_LABELS[nextDir].toLowerCase()}`}
      >
        <span aria-hidden="true">{effectiveDir === "asc" ? "↑ Asc" : "↓ Desc"}</span>
      </Button>
    </div>
  );
}
