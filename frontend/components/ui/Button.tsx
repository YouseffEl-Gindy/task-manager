import type { ComponentPropsWithRef } from "react";

export type ButtonVariant = "primary" | "secondary" | "danger" | "ghost";

const BASE =
  "inline-flex items-center justify-center gap-2 rounded-md px-4 py-2 text-sm font-medium transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 disabled:cursor-not-allowed disabled:opacity-60";

const VARIANTS: Record<ButtonVariant, string> = {
  primary: "bg-blue-700 text-white hover:bg-blue-800 focus-visible:outline-blue-700",
  secondary:
    "border border-slate-300 bg-white text-slate-900 hover:bg-slate-50 focus-visible:outline-blue-700",
  danger: "bg-red-700 text-white hover:bg-red-800 focus-visible:outline-red-700",
  ghost: "text-slate-700 hover:bg-slate-100 focus-visible:outline-blue-700",
};

/** Class names for a button look-alike (e.g. a Next.js Link). */
export function buttonClassName(variant: ButtonVariant = "primary", extra = ""): string {
  return `${BASE} ${VARIANTS[variant]} ${extra}`.trim();
}

export interface ButtonProps extends ComponentPropsWithRef<"button"> {
  variant?: ButtonVariant;
  /** Disables the button and marks it busy while an action runs. */
  isLoading?: boolean;
}

export function Button({
  variant = "primary",
  isLoading = false,
  disabled,
  className = "",
  type = "button",
  children,
  ...rest
}: ButtonProps) {
  return (
    <button
      type={type}
      className={buttonClassName(variant, className)}
      disabled={disabled || isLoading}
      aria-busy={isLoading || undefined}
      {...rest}
    >
      {children}
    </button>
  );
}
