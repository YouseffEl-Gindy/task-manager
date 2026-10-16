import { ServerUnavailableError, getErrorMessage, isApiErrorWithCode } from "@/lib/api/client";
import { ErrorMessage } from "./ErrorMessage";
import { Spinner } from "./Spinner";

/**
 * The error state for a failed load.
 * - `unauthorized`: AuthGuard is redirecting to /login, so just show progress.
 * - server unavailable: the "starting up" message with a retry (AC-ERR-5).
 * - anything else: the API message with a retry.
 */
export function LoadError({ error, onRetry }: { error: unknown; onRetry: () => void }) {
  if (isApiErrorWithCode(error, "unauthorized")) {
    return <Spinner label="Redirecting to log in…" />;
  }
  const title = error instanceof ServerUnavailableError ? "Can't reach the server" : "Couldn't load this page";
  return <ErrorMessage title={title} message={getErrorMessage(error)} onRetry={onRetry} />;
}
