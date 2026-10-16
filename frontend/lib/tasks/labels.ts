import type { SortDirection, TaskItemPriority, TaskItemStatus, TaskSortBy } from "@/types/api";

export const STATUS_LABELS: Record<TaskItemStatus, string> = {
  todo: "To do",
  inProgress: "In progress",
  done: "Done",
};

export const PRIORITY_LABELS: Record<TaskItemPriority, string> = {
  low: "Low",
  medium: "Medium",
  high: "High",
};

export const SORT_BY_LABELS: Record<TaskSortBy, string> = {
  createdAt: "Created date",
  dueDate: "Due date",
  priority: "Priority",
  title: "Title",
};

export const SORT_DIR_LABELS: Record<SortDirection, string> = {
  asc: "Ascending",
  desc: "Descending",
};
