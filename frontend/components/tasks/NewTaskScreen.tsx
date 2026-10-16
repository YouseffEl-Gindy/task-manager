"use client";

import { useRouter } from "next/navigation";
import type { UpdateTaskRequest } from "@/types/api";
import { createTask } from "@/lib/api/tasks";
import { TaskForm } from "./TaskForm";

/** /tasks/new: TaskForm in create mode; goes to /tasks on success. */
export function NewTaskScreen() {
  const router = useRouter();

  async function handleSubmit(values: UpdateTaskRequest) {
    await createTask(values);
    router.push("/tasks");
  }

  return (
    <div className="mx-auto flex w-full max-w-2xl flex-col gap-6">
      <h1 className="text-2xl font-bold text-slate-900">New task</h1>
      <div className="rounded-lg border border-slate-200 bg-white p-4 sm:p-6">
        <TaskForm mode="create" onSubmit={handleSubmit} />
      </div>
    </div>
  );
}
