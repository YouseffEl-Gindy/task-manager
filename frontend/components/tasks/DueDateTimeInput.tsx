"use client";

import { useId } from "react";
import {
  dateToLocalInputValue,
  localInputValueToUtcIso,
  localTimeZoneName,
  utcIsoToLocalInputValue,
} from "@/lib/tasks/dateTime";
import { INPUT_CLASS } from "@/components/ui/TextField";
import { Button } from "@/components/ui/Button";

export interface DueDateTimeInputProps {
  label?: string;
  /** UTC ISO string ("...Z") or null for no due date. */
  value: string | null;
  /** Receives a UTC ISO string ("...Z", whole seconds) or null. */
  onChange: (value: string | null) => void;
  error?: string;
  /** Sets the picker's `min` to the current local time (create mode). */
  minNow?: boolean;
}

/**
 * `<input type="datetime-local">` that shows and accepts local time and talks
 * UTC to its parent. Includes a button that clears the value (null).
 */
export function DueDateTimeInput({ label = "Due date and time", value, onChange, error, minNow = false }: DueDateTimeInputProps) {
  const id = useId();
  const hintId = `${id}-hint`;
  const errorId = `${id}-error`;
  const describedBy = error ? `${hintId} ${errorId}` : hintId;

  return (
    <div className="flex flex-col gap-1">
      <label htmlFor={id} className="text-sm font-medium text-slate-800">
        {label} <span className="font-normal text-slate-500">(optional)</span>
      </label>
      <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
        <input
          id={id}
          type="datetime-local"
          className={`${INPUT_CLASS} sm:max-w-xs`}
          value={utcIsoToLocalInputValue(value)}
          min={minNow ? dateToLocalInputValue(new Date()).slice(0, 16) : undefined}
          aria-invalid={error ? true : undefined}
          aria-describedby={describedBy}
          onChange={(event) => onChange(event.target.value === "" ? null : localInputValueToUtcIso(event.target.value))}
        />
        {value !== null ? (
          <Button variant="ghost" onClick={() => onChange(null)}>
            Clear due date
          </Button>
        ) : null}
      </div>
      <p id={hintId} className="text-xs text-slate-600">
        In your local time ({localTimeZoneName()}).
      </p>
      {error ? (
        <p id={errorId} className="text-sm text-red-700">
          {error}
        </p>
      ) : null}
    </div>
  );
}
