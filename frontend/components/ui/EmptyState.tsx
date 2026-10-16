import type { ReactNode } from "react";

export interface EmptyStateProps {
  title: string;
  description?: string;
  /** A call to action, such as a link or button. */
  action?: ReactNode;
}

export function EmptyState({ title, description, action }: EmptyStateProps) {
  return (
    <div className="rounded-lg border border-dashed border-slate-300 bg-white px-6 py-10 text-center">
      <p className="text-base font-semibold text-slate-900">{title}</p>
      {description ? <p className="mt-1 text-sm text-slate-600">{description}</p> : null}
      {action ? <div className="mt-4 flex justify-center">{action}</div> : null}
    </div>
  );
}
