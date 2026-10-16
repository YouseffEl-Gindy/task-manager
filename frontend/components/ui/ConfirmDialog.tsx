"use client";

import { useEffect, useId, useRef, type KeyboardEvent, type ReactNode } from "react";
import { Button } from "./Button";

export interface ConfirmDialogProps {
  open: boolean;
  title: string;
  children?: ReactNode;
  confirmLabel?: string;
  cancelLabel?: string;
  /** Shows the confirm button as busy and disables both buttons. */
  isConfirming?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

/**
 * Accessible modal confirmation: role="alertdialog", focus moves to Cancel on open,
 * Escape cancels, Tab stays inside, and focus returns to the opener on close.
 */
export function ConfirmDialog({
  open,
  title,
  children,
  confirmLabel = "Confirm",
  cancelLabel = "Cancel",
  isConfirming = false,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  const titleId = useId();
  const descriptionId = useId();
  const dialogRef = useRef<HTMLDivElement>(null);
  const cancelRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    if (!open) {
      return;
    }
    const previouslyFocused = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    cancelRef.current?.focus();
    return () => {
      previouslyFocused?.focus();
    };
  }, [open]);

  if (!open) {
    return null;
  }

  function handleKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    if (event.key === "Escape" && !isConfirming) {
      event.stopPropagation();
      onCancel();
      return;
    }
    if (event.key === "Tab" && dialogRef.current) {
      const focusable = Array.from(dialogRef.current.querySelectorAll<HTMLElement>("button:not([disabled])"));
      if (focusable.length === 0) {
        return;
      }
      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4">
      <div
        ref={dialogRef}
        role="alertdialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={children ? descriptionId : undefined}
        className="w-full max-w-md rounded-lg bg-white p-6 shadow-xl"
        onKeyDown={handleKeyDown}
      >
        <h2 id={titleId} className="text-lg font-semibold text-slate-900">
          {title}
        </h2>
        {children ? (
          <div id={descriptionId} className="mt-2 text-sm text-slate-700">
            {children}
          </div>
        ) : null}
        <div className="mt-6 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
          <Button ref={cancelRef} variant="secondary" onClick={onCancel} disabled={isConfirming}>
            {cancelLabel}
          </Button>
          <Button variant="danger" onClick={onConfirm} isLoading={isConfirming}>
            {confirmLabel}
          </Button>
        </div>
      </div>
    </div>
  );
}
