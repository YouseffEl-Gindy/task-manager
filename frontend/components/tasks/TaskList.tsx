import Link from "next/link";
import type { TaskDto } from "@/types/api";
import { Button, buttonClassName } from "@/components/ui/Button";
import { EmptyState } from "@/components/ui/EmptyState";
import { TaskListItem } from "./TaskListItem";

export interface TaskListProps {
  tasks: readonly TaskDto[];
  /** True when status/priority/search narrow the list. */
  filtersActive: boolean;
  onClearFilters: () => void;
  onTaskUpdated: (task: TaskDto) => void;
}

export function TaskList({ tasks, filtersActive, onClearFilters, onTaskUpdated }: TaskListProps) {
  if (tasks.length === 0) {
    return filtersActive ? (
      <EmptyState
        title="No tasks match your filters"
        description="Try a different search or filter."
        action={
          <Button variant="secondary" onClick={onClearFilters}>
            Clear filters
          </Button>
        }
      />
    ) : (
      <EmptyState
        title="You have no tasks yet"
        description="Tasks you create will show up here."
        action={
          <Link href="/tasks/new" className={buttonClassName("primary")}>
            Create your first task
          </Link>
        }
      />
    );
  }

  return (
    <ul className="flex flex-col gap-3" aria-label="Tasks">
      {tasks.map((task) => (
        <TaskListItem key={task.id} task={task} onTaskUpdated={onTaskUpdated} />
      ))}
    </ul>
  );
}
