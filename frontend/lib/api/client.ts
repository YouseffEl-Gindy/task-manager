/**
 * The single place that talks HTTP to the backend.
 *
 * - Relative base `/api/v1` on this origin (Next.js rewrites proxy it to the backend).
 * - Same-origin credentials, so the httpOnly `tm_session` cookie is sent automatically.
 * - `X-CSRF-Protection: 1` on every unsafe method (POST/PUT/PATCH/DELETE).
 * - Error bodies are never assumed to be JSON. A valid `ApiError` body becomes an
 *   `ApiResponseError`; anything else (network failure, proxy 502/503/504, HTML
 *   "site stopped" page, malformed JSON) becomes a `ServerUnavailableError`.
 * - No navigation happens here. A `401 unauthorized` notifies subscribers
 *   (`onUnauthorized`), and the auth layer decides what to do.
 */
import {
  API_BASE_PATH,
  CSRF_HEADER_NAME,
  CSRF_HEADER_VALUE,
  type ApiError,
  type ApiErrorCode,
} from "@/types/api";

export const SERVER_UNAVAILABLE_MESSAGE =
  "The server is starting up or unavailable. Please try again in a moment.";

export const API_ERROR_CODES = [
  "validation_failed",
  "bad_request",
  "invalid_credentials",
  "unauthorized",
  "csrf_failed",
  "not_found",
  "email_taken",
  "rate_limited",
  "internal_error",
] as const satisfies readonly ApiErrorCode[];

export type HttpMethod = "GET" | "POST" | "PUT" | "PATCH" | "DELETE";

/** The backend answered with a well-formed `ApiError` body. Branch on `code`. */
export class ApiResponseError extends Error {
  readonly apiError: ApiError;

  constructor(apiError: ApiError) {
    super(apiError.message);
    this.name = "ApiResponseError";
    this.apiError = apiError;
  }

  get status(): number {
    return this.apiError.status;
  }

  get code(): ApiErrorCode {
    return this.apiError.code;
  }

  get fieldErrors(): Record<string, string[]> | null {
    return this.apiError.errors;
  }
}

/**
 * The response did not come from the backend's error pipeline, or there was no
 * response at all (cold start, proxy error, quota page, network failure).
 */
export class ServerUnavailableError extends Error {
  /** HTTP status if a response arrived, `null` for network failures. */
  readonly status: number | null;

  constructor(status: number | null) {
    super(SERVER_UNAVAILABLE_MESSAGE);
    this.name = "ServerUnavailableError";
    this.status = status;
  }
}

// ---------- 401 notification ----------

type UnauthorizedListener = () => void;
const unauthorizedListeners = new Set<UnauthorizedListener>();

/** Subscribe to `401 unauthorized` responses (session missing or expired). Returns an unsubscribe function. */
export function onUnauthorized(listener: UnauthorizedListener): () => void {
  unauthorizedListeners.add(listener);
  return () => {
    unauthorizedListeners.delete(listener);
  };
}

function notifyUnauthorized(): void {
  for (const listener of [...unauthorizedListeners]) {
    listener();
  }
}

// ---------- Helpers ----------

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function isApiErrorCode(value: unknown): value is ApiErrorCode {
  return typeof value === "string" && (API_ERROR_CODES as readonly string[]).includes(value);
}

function isFieldErrors(value: unknown): value is Record<string, string[]> {
  return (
    isRecord(value) &&
    Object.values(value).every(
      (messages) => Array.isArray(messages) && messages.every((m) => typeof m === "string"),
    )
  );
}

/** Runtime check that a parsed body really is an `ApiError`. */
export function isApiError(value: unknown): value is ApiError {
  return (
    isRecord(value) &&
    typeof value.status === "number" &&
    isApiErrorCode(value.code) &&
    typeof value.message === "string" &&
    typeof value.traceId === "string" &&
    (value.errors === null || isFieldErrors(value.errors))
  );
}

/** True for a cancelled fetch. DOMException is not always an `Error` instance, so check the name. */
export function isAbortError(error: unknown): boolean {
  return typeof error === "object" && error !== null && "name" in error && error.name === "AbortError";
}

function isJsonContentType(response: Response): boolean {
  const contentType = response.headers.get("content-type") ?? "";
  return /\bapplication\/([\w.+-]*\+)?json\b/i.test(contentType);
}

/** Reads a body as JSON without ever throwing. Returns `undefined` if it isn't JSON. */
async function readJson(response: Response): Promise<unknown> {
  if (!isJsonContentType(response)) {
    return undefined;
  }
  let text: string;
  try {
    text = await response.text();
  } catch {
    return undefined;
  }
  if (text.trim() === "") {
    return undefined;
  }
  try {
    return JSON.parse(text) as unknown;
  } catch {
    return undefined;
  }
}

export function buildUrl(path: string, query?: URLSearchParams): string {
  const normalizedPath = path.startsWith("/") ? path : `/${path}`;
  const queryString = query?.toString();
  return `${API_BASE_PATH}${normalizedPath}${queryString ? `?${queryString}` : ""}`;
}

export interface RequestOptions {
  method?: HttpMethod;
  /** Serialized as JSON. */
  body?: unknown;
  query?: URLSearchParams;
  signal?: AbortSignal;
}

async function send(path: string, options: RequestOptions): Promise<Response> {
  const method = options.method ?? "GET";
  const headers = new Headers({ Accept: "application/json" });
  if (method !== "GET") {
    headers.set(CSRF_HEADER_NAME, CSRF_HEADER_VALUE);
  }
  const init: RequestInit = {
    method,
    headers,
    credentials: "same-origin",
    cache: "no-store",
    signal: options.signal,
  };
  if (options.body !== undefined) {
    headers.set("Content-Type", "application/json");
    init.body = JSON.stringify(options.body);
  }

  let response: Response;
  try {
    response = await fetch(buildUrl(path, options.query), init);
  } catch (error) {
    if (isAbortError(error)) {
      throw error;
    }
    throw new ServerUnavailableError(null);
  }

  if (!response.ok) {
    const body = await readJson(response);
    if (isApiError(body)) {
      if (body.code === "unauthorized") {
        notifyUnauthorized();
      }
      throw new ApiResponseError(body);
    }
    throw new ServerUnavailableError(response.status);
  }
  return response;
}

/** Sends a request and returns the parsed JSON body (2xx with a JSON body expected). */
export async function requestJson<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const response = await send(path, options);
  const body = await readJson(response);
  if (body === undefined) {
    // A 2xx that isn't JSON did not come from the API (for example an HTML page).
    throw new ServerUnavailableError(response.status);
  }
  return body as T;
}

/** Sends a request that answers `204 No Content` (or any 2xx whose body is ignored). */
export async function requestVoid(path: string, options: RequestOptions = {}): Promise<void> {
  const response = await send(path, options);
  try {
    await response.body?.cancel();
  } catch {
    // Nothing to clean up.
  }
}

// ---------- Error helpers for components ----------

export function isApiErrorWithCode(error: unknown, code: ApiErrorCode): error is ApiResponseError {
  return error instanceof ApiResponseError && error.code === code;
}

/** First message per field from a `validation_failed` error. */
export function getFieldErrors(error: unknown): Record<string, string> {
  if (!(error instanceof ApiResponseError) || error.fieldErrors === null) {
    return {};
  }
  const result: Record<string, string> = {};
  for (const [field, messages] of Object.entries(error.fieldErrors)) {
    if (messages.length > 0) {
      result[field] = messages[0];
    }
  }
  return result;
}

/** A user-facing message for any thrown error. */
export function getErrorMessage(error: unknown): string {
  if (error instanceof ApiResponseError) {
    return error.apiError.message;
  }
  if (error instanceof ServerUnavailableError) {
    return SERVER_UNAVAILABLE_MESSAGE;
  }
  return "Something went wrong. Please try again.";
}
