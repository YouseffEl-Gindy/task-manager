"use client";

import Link from "next/link";
import { useState, type FormEvent } from "react";
import {
  TASK_ITEM_PRIORITIES,
  TASK_ITEM_STATUSES,
  type TaskDto,
  type TaskItemPriority,
  type TaskItemStatus,
  type UpdateTaskRequest,
} from "@/types/api";
import { mapServerError } from "@/lib/forms/serverErrors";
import { isInFuture, isSameInstant } from "@/lib/tasks/dateTime";
import { PRIORITY_LABELS, STATUS_LABELS } from "@/lib/tasks/labels";
import {
  DESCRIPTION_MAX,
  TITLE_MAX,
  collectErrors,
  hasErrors,
  validateDescription,
  validateTitle,
} from "@/lib/validation/rules";
import { Button, buttonClassName } from "@/components/ui/Button";
import { FormError } from "@/components/ui/FormError";
import { Select } from "@/components/ui/Select";
import { TextArea } from "@/components/ui/TextArea";
import { TextField } from "@/components/ui/TextField";
import { DueDateTimeInput } from "./DueDateTimeInput";

type Field = "title" | "description" | "status" | "priority" | "dueDate";
const FIELDS: readonly Field[] = ["title", "description", "status", "priority", "dueDate"];

const STATUS_OPTIONS = TASK_ITEM_STATUSES.map((value) => ({ value, label: STATUS_LABELS[value] }));
const PRIORITY_OPTIONS = TASK_ITEM_PRIORITIES.map((value) => ({ value, label: PRIORITY_LABELS[value] }));

export const DUE_DATE_PAST_MESSAGE = "Due date must be in the future.";

export interface TaskFormProps {
  mode: "create" | "edit";
  /** Required in edit mode: the stored task. */
  initialTask?: TaskDto;
  /** Receives every field (a valid CreateTaskRequest and UpdateTaskRequest). Throw to show server errors. */
  onSubmit: (values: UpdateTaskRequest) => Promise<void>;
  /** Injectable for tests. */
  now?: () => Date;
}

/**
 * Create/edit form. A non-null due date must be in the future on create; on edit
 * only when it changed (FR-T10), so an overdue task can still be edited.
 */
export function TaskForm({ mode, initialTask, onSubmit, now = () => new Date() }: TaskFormProps) {
  const initialDueDate = initialTask?.dueDate ?? null;
  const [title, setTitle] = useState(initialTask?.title ?? "");
  const [description, setDescription] = useState(initialTask?.description ?? "");
  const [status, setStatus] = useState<TaskItemStatus>(initialTask?.status ?? "todo");
  const [priority, setPriority] = useState<TaskItemPriority>(initialTask?.priority ?? "medium");
  const [dueDate, setDueDate] = useState<string | null>(initialDueDate);
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<Field, string>>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const dueDateUnchanged = mode === "edit" && isSameInstant(dueDate, initialDueDate);

  function validateDueDate(): string | null {
    if (dueDate === null || dueDateUnchanged) {
      return null;
    }
    return isInFuture(dueDate, now()) ? null : DUE_DATE_PAST_MESSAGE;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const errors = collectErrors<Field>({
      title: validateTitle(title),
      description: validateDescription(description),
      status: null,
      priority: null,
      dueDate: validateDueDate(),
    });
    setFieldErrors(errors);
    setFormError(null);
    if (hasErrors(errors)) {
      return;
    }
    const trimmedDescription = description.trim();
    const values: UpdateTaskRequest = {
      title: title.trim(),
      description: trimmedDescription === "" ? null : trimmedDescription,
      status,
      priority,
      // Re-send the stored string untouched when unchanged, so the server sees the same instant.
      dueDate: dueDateUnchanged ? initialDueDate : dueDate,
    };
    setSubmitting(true);
    try {
      await onSubmit(values);
    } catch (error) {
      const mapped = mapServerError(error, FIELDS);
      setFieldErrors(mapped.fieldErrors);
      setFormError(mapped.formError);
      setSubmitting(false);
    }
  }

  return (
    <form
      noValidate
      onSubmit={handleSubmit}
      className="flex flex-col gap-5"
      aria-label={mode === "create" ? "New task" : "Edit task"}
    >
      <FormError message={formError} />
      <TextField
        label="Title"
        name="title"
        required
        maxLength={TITLE_MAX + 50}
        value={title}
        onChange={(e) => setTitle(e.target.value)}
        error={fieldErrors.title}
      />
      <TextArea
        label="Description (optional)"
        name="description"
        maxLength={DESCRIPTION_MAX + 100}
        value={description}
        onChange={(e) => setDescription(e.target.value)}
        error={fieldErrors.description}
      />
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <Select<TaskItemStatus>
          label="Status"
          options={STATUS_OPTIONS}
          value={status}
          onValueChange={setStatus}
          error={fieldErrors.status}
        />
        <Select<TaskItemPriority>
          label="Priority"
          options={PRIORITY_OPTIONS}
          value={priority}
          onValueChange={setPriority}
          error={fieldErrors.priority}
        />
      </div>
      <DueDateTimeInput value={dueDate} onChange={setDueDate} error={fieldErrors.dueDate} minNow={mode === "create"} />
      <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
        <Link href="/tasks" className={buttonClassName("secondary")}>
          Cancel
        </Link>
        <Button type="submit" isLoading={submitting}>
          {mode === "create" ? (submitting ? "Creating…" : "Create task") : submitting ? "Saving…" : "Save changes"}
        </Button>
      </div>
    </form>
  );
}
