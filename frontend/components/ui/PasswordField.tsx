"use client";

import { useState } from "react";
import { TextField, type TextFieldProps } from "./TextField";

export type PasswordFieldProps = Omit<TextFieldProps, "type" | "adornment">;

/** A password input with an accessible show/hide toggle. The value is never trimmed. */
export function PasswordField(props: PasswordFieldProps) {
  const [visible, setVisible] = useState(false);
  return (
    <TextField
      {...props}
      type={visible ? "text" : "password"}
      autoCapitalize="none"
      spellCheck={false}
      adornment={
        <button
          type="button"
          className="rounded px-2 py-1 text-xs font-medium text-slate-700 hover:bg-slate-100 focus-visible:outline-2 focus-visible:outline-blue-700"
          aria-label={visible ? `Hide ${props.label.toLowerCase()}` : `Show ${props.label.toLowerCase()}`}
          aria-pressed={visible}
          onClick={() => setVisible((v) => !v)}
        >
          {visible ? "Hide" : "Show"}
        </button>
      }
    />
  );
}
