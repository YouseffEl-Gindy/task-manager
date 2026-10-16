"use client";

import { useCallback, useEffect, useState } from "react";
import type { TaskDto } from "@/types/api";
import { isAbortError } from "@/lib/api/client";
import { getTask } from "@/lib/api/tasks";

export type TaskState =
  | { status: "loading" }
  | { status: "error"; error: unknown }
  | { status: "success"; task: TaskDto };

type Result = { key: string; task: TaskDto } | { key: string; error: unknown };

/** Loads GET /tasks/{id}. Re-fetches when the id changes or on `reload()`. */
export function useTask(id: string) {
  const [reloadCount, setReloadCount] = useState(0);
  const [result, setResult] = useState<Result | null>(null);
  const requestKey = `${id}#${reloadCount}`;

  useEffect(() => {
    const controller = new AbortController();
    const taskId = requestKey.slice(0, requestKey.lastIndexOf("#"));
    getTask(taskId, controller.signal).then(
      (task) => setResult({ key: requestKey, task }),
      (error: unknown) => {
        if (!isAbortError(error)) {
          setResult({ key: requestKey, error });
        }
      },
    );
    return () => controller.abort();
  }, [requestKey]);

  const reload = useCallback(() => setReloadCount((n) => n + 1), []);

  let state: TaskState;
  if (result === null || result.key !== requestKey) {
    state = { status: "loading" };
  } else if ("task" in result) {
    state = { status: "success", task: result.task };
  } else {
    state = { status: "error", error: result.error };
  }
  return { state, reload };
}
