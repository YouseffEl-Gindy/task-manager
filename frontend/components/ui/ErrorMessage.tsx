import { Button } from "./Button";

export interface ErrorMessageProps {
  title?: string;
  message: string;
  /** When set, shows a "Try again" button. */
  onRetry?: () => void;
}

/** A page-level error block, e.g. a failed load, with an optional retry. */
export function ErrorMessage({ title = "Something went wrong", message, onRetry }: ErrorMessageProps) {
  return (
    <div role="alert" className="rounded-lg border border-red-200 bg-red-50 p-4 text-red-900">
      <p className="font-semibold">{title}</p>
      <p className="mt-1 text-sm">{message}</p>
      {onRetry ? (
        <Button variant="secondary" className="mt-3" onClick={onRetry}>
          Try again
        </Button>
      ) : null}
    </div>
  );
}
