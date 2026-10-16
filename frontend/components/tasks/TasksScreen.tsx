"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useCallback, useMemo } from "react";
import type { TaskListQuery } from "@/types/api";
import { hasActiveFilters, parseTaskListQuery, tasksHref } from "@/lib/tasks/queryParams";
import { useTaskList } from "@/lib/tasks/useTaskList";
import { buttonClassName } from "@/components/ui/Button";
import { LoadError } from "@/components/ui/LoadError";
import { Spinner } from "@/components/ui/Spinner";
import { TaskFilters } from "./TaskFilters";
import { TaskList } from "./TaskList";
import { TaskSearchInput } from "./TaskSearchInput";
import { TaskSortControl } from "./TaskSortControl";

/** /tasks: filter/search/sort state lives in the URL (SPEC 8.3). */
export function TasksScreen() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const query = useMemo(() => parseTaskListQuery(searchParams), [searchParams]);
  const { state, reload, replaceTask } = useTaskList(query);

  const updateQuery = useCallback(
    (patch: Partial<TaskListQuery>) => {
      router.replace(tasksHref({ ...query, ...patch }), { scroll: false });
    },
    [query, router],
  );

  const handleSearchChange = useCallback((search: string) => updateQuery({ search: search || undefined }), [updateQuery]);

  const clearFilters = useCallback(
    () => updateQuery({ status: undefined, priority: undefined, search: undefined }),
    [updateQuery],
  );

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-bold text-slate-900">Tasks</h1>
        <Link href="/tasks/new" className={buttonClassName("primary")}>
          New task
        </Link>
      </div>

      <section aria-label="Filter and sort tasks" className="grid grid-cols-1 gap-4 rounded-lg border border-slate-200 bg-white p-4 sm:grid-cols-2 lg:grid-cols-4">
        <TaskSearchInput value={query.search ?? ""} onSearchChange={handleSearchChange} />
        <TaskFilters
          status={query.status}
          priority={query.priority}
          onStatusChange={(status) => updateQuery({ status })}
          onPriorityChange={(priority) => updateQuery({ priority })}
        />
        <TaskSortControl
          sortBy={query.sortBy}
          sortDir={query.sortDir}
          onSortByChange={(sortBy) => updateQuery({ sortBy })}
          onSortDirChange={(sortDir) => updateQuery({ sortDir })}
        />
      </section>

      <section aria-live="polite" aria-busy={state.status === "loading"}>
        {state.status === "loading" ? <Spinner label="Loading tasks…" /> : null}
        {state.status === "error" ? <LoadError error={state.error} onRetry={reload} /> : null}
        {state.status === "success" ? (
          <TaskList
            tasks={state.tasks}
            filtersActive={hasActiveFilters(query)}
            onClearFilters={clearFilters}
            onTaskUpdated={replaceTask}
          />
        ) : null}
      </section>
    </div>
  );
}
