"use client";

import { useId, type TextareaHTMLAttributes } from "react";
import { INPUT_CLASS } from "./TextField";

export interface TextAreaProps extends Omit<TextareaHTMLAttributes<HTMLTextAreaElement>, "id"> {
  label: string;
  error?: string;
  hint?: string;
  id?: string;
}

export function TextArea({ label, error, hint, id, className = "", ...rest }: TextAreaProps) {
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
      <textarea
        id={inputId}
        className={`${INPUT_CLASS} min-h-28`}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy}
        {...rest}
      />
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
