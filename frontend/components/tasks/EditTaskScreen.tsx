"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import type { UpdateTaskRequest } from "@/types/api";
import { isApiErrorWithCode } from "@/lib/api/client";
import { updateTask } from "@/lib/api/tasks";
import { useTask } from "@/lib/tasks/useTask";
import { buttonClassName } from "@/components/ui/Button";
import { EmptyState } from "@/components/ui/EmptyState";
import { LoadError } from "@/components/ui/LoadError";
import { Spinner } from "@/components/ui/Spinner";
import { DeleteTaskButton } from "./DeleteTaskButton";
import { TaskForm } from "./TaskForm";

/** /tasks/[id]: TaskForm in edit mode plus DeleteTaskButton. Shows "Task not found" on a 404. */
export function EditTaskScreen({ id }: { id: string }) {
  const router = useRouter();
  const { state, reload } = useTask(id);

  async function handleSubmit(values: UpdateTaskRequest) {
    await updateTask(id, values);
    router.push("/tasks");
  }

  let content;
  if (state.status === "loading") {
    content = <Spinner label="Loading task…" />;
  } else if (state.status === "error") {
    content = isApiErrorWithCode(state.error, "not_found") ? (
      <EmptyState
        title="Task not found"
        description="It may have been deleted, or the link is wrong."
        action={
          <Link href="/tasks" className={buttonClassName("secondary")}>
            Back to tasks
          </Link>
        }
      />
    ) : (
      <LoadError error={state.error} onRetry={reload} />
    );
  } else {
    content = (
      <>
        <div className="rounded-lg border border-slate-200 bg-white p-4 sm:p-6">
          <TaskForm key={state.task.id} mode="edit" initialTask={state.task} onSubmit={handleSubmit} />
        </div>
        <div className="rounded-lg border border-red-200 bg-white p-4 sm:p-6">
          <DeleteTaskButton taskId={state.task.id} taskTitle={state.task.title} />
        </div>
      </>
    );
  }

  return (
    <div className="mx-auto flex w-full max-w-2xl flex-col gap-6">
      <h1 className="text-2xl font-bold text-slate-900">Edit task</h1>
      {content}
    </div>
  );
}
