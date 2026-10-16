"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { getErrorMessage } from "@/lib/api/client";
import { deleteTask } from "@/lib/api/tasks";
import { Button } from "@/components/ui/Button";
import { ConfirmDialog } from "@/components/ui/ConfirmDialog";

export interface DeleteTaskButtonProps {
  taskId: string;
  taskTitle: string;
}

/** Asks for confirmation, then DELETE /tasks/{id}, then goes to /tasks. */
export function DeleteTaskButton({ taskId, taskTitle }: DeleteTaskButtonProps) {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleConfirm() {
    setDeleting(true);
    setError(null);
    try {
      await deleteTask(taskId);
      setOpen(false);
      router.push("/tasks");
    } catch (err) {
      setError(getErrorMessage(err));
      setOpen(false);
      setDeleting(false);
    }
  }

  return (
    <div className="flex flex-col gap-2">
      <div>
        <Button variant="danger" onClick={() => setOpen(true)}>
          Delete task
        </Button>
      </div>
      {error ? (
        <p role="alert" className="text-sm text-red-700">
          {error}
        </p>
      ) : null}
      <ConfirmDialog
        open={open}
        title="Delete this task?"
        confirmLabel="Delete task"
        isConfirming={deleting}
        onConfirm={handleConfirm}
        onCancel={() => setOpen(false)}
      >
        &ldquo;{taskTitle}&rdquo; will be permanently deleted.
      </ConfirmDialog>
    </div>
  );
}
