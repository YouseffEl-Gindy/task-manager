"use client";

import { useId, type InputHTMLAttributes, type ReactNode } from "react";

export interface TextFieldProps extends Omit<InputHTMLAttributes<HTMLInputElement>, "id"> {
  label: string;
  /** Error message shown under the input; also sets aria-invalid. */
  error?: string;
  /** Helper text shown under the input. */
  hint?: ReactNode;
  /** Optional element rendered at the right edge of the input (e.g. a show/hide toggle). */
  adornment?: ReactNode;
  id?: string;
}

export const INPUT_CLASS =
  "block w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm placeholder:text-slate-400 focus:border-blue-700 focus:outline-2 focus:outline-blue-700 aria-[invalid=true]:border-red-700 disabled:bg-slate-100";

export function TextField({ label, error, hint, adornment, id, className = "", ...inputProps }: TextFieldProps) {
  const generatedId = useId();
  const inputId = id ?? generatedId;
  const hintId = hint ? `${inputId}-hint` : undefined;
  const errorId = error ? `${inputId}-error` : undefined;
  const describedBy = [hintId, errorId].filter(Boolean).join(" ") || undefined;

  return (
    <div className={`flex flex-col gap-1 ${className}`.trim()}>
      <label htmlFor={inputId} className="text-sm font-medium text-slate-800">
        {label}
      </label>
      <div className="relative">
        <input
          id={inputId}
          className={`${INPUT_CLASS} ${adornment ? "pr-20" : ""}`.trim()}
          aria-invalid={error ? true : undefined}
          aria-describedby={describedBy}
          {...inputProps}
        />
        {adornment ? <div className="absolute inset-y-0 right-1 flex items-center">{adornment}</div> : null}
      </div>
      {hint ? (
        <p id={hintId} className="text-xs text-slate-600">
          {hint}
        </p>
      ) : null}
      {error ? (
        <p id={errorId} className="text-sm text-red-700">
          {error}
        </p>
      ) : null}
    </div>
  );
}
