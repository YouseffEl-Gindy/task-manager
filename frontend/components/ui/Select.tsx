"use client";

import { useId, type SelectHTMLAttributes } from "react";
import { INPUT_CLASS } from "./TextField";

export interface SelectOption<T extends string> {
  value: T;
  label: string;
}

export interface SelectProps<T extends string>
  extends Omit<SelectHTMLAttributes<HTMLSelectElement>, "id" | "value" | "onChange"> {
  label: string;
  /** Visually hide the label (it stays available to screen readers). */
  hideLabel?: boolean;
  options: readonly SelectOption<T>[];
  value: T;
  onValueChange: (value: T) => void;
  error?: string;
  id?: string;
}

export function Select<T extends string>({
  label,
  hideLabel = false,
  options,
  value,
  onValueChange,
  error,
  id,
  className = "",
  ...rest
}: SelectProps<T>) {
  const generatedId = useId();
  const selectId = id ?? generatedId;
  const errorId = error ? `${selectId}-error` : undefined;

  return (
    <div className={`flex flex-col gap-1 ${className}`.trim()}>
      <label htmlFor={selectId} className={hideLabel ? "sr-only" : "text-sm font-medium text-slate-800"}>
        {label}
      </label>
      <select
        id={selectId}
        className={INPUT_CLASS}
        value={value}
        aria-invalid={error ? true : undefined}
        aria-describedby={errorId}
        onChange={(event) => {
          const next = options.find((option) => option.value === event.target.value);
          if (next) {
            onValueChange(next.value);
          }
        }}
        {...rest}
      >
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
      {error ? (
        <p id={errorId} className="text-sm text-red-700">
          {error}
        </p>
      ) : null}
    </div>
  );
}
