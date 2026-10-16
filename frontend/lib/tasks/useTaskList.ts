"use client";

import { useCallback, useEffect, useState } from "react";
import type { TaskDto, TaskListQuery } from "@/types/api";
import { isAbortError } from "@/lib/api/client";
import { listTasks } from "@/lib/api/tasks";
import { parseTaskListQuery, queryKey } from "./queryParams";

export type TaskListState =
  | { status: "loading" }
  | { status: "error"; error: unknown }
  | { status: "success"; tasks: TaskDto[] };

type Result = { key: string; tasks: TaskDto[] } | { key: string; error: unknown };

/** Loads GET /tasks for the query. Re-fetches when the query changes or on `reload()`. */
export function useTaskList(query: TaskListQuery) {
  const [reloadCount, setReloadCount] = useState(0);
  const [result, setResult] = useState<Result | null>(null);
  const requestKey = `${queryKey(query)}#${reloadCount}`;

  useEffect(() => {
    const controller = new AbortController();
    const params = parseTaskListQuery(new URLSearchParams(requestKey.split("#")[0]));
    listTasks(params, controller.signal).then(
      (tasks) => setResult({ key: requestKey, tasks }),
      (error: unknown) => {
        if (!isAbortError(error)) {
          setResult({ key: requestKey, error });
        }
      },
    );
    return () => controller.abort();
  }, [requestKey]);

  const reload = useCallback(() => setReloadCount((n) => n + 1), []);

  const replaceTask = useCallback((updated: TaskDto) => {
    setResult((current) =>
      current && "tasks" in current
        ? { ...current, tasks: current.tasks.map((task) => (task.id === updated.id ? updated : task)) }
        : current,
    );
  }, []);

  let state: TaskListState;
  if (result === null || result.key !== requestKey) {
    state = { status: "loading" };
  } else if ("tasks" in result) {
    state = { status: "success", tasks: result.tasks };
  } else {
    state = { status: "error", error: result.error };
  }

  return { state, reload, replaceTask };
}
