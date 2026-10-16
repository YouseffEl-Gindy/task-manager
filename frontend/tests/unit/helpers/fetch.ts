import { vi } from "vitest";
import type { ApiError, ApiErrorCode } from "@/types/api";

export interface RecordedRequest {
  url: URL;
  method: string;
  headers: Headers;
  body: unknown;
}

export type FetchHandler = (request: RecordedRequest) => Response | Promise<Response>;

/** Replaces global fetch with a handler. Returns the mock plus a list of recorded requests. */
export function installFetch(handler: FetchHandler) {
  const requests: RecordedRequest[] = [];
  const mock = vi.fn(async (input: RequestInfo | URL, init: RequestInit = {}) => {
    const rawBody = init.body;
    const request: RecordedRequest = {
      url: new URL(String(input), "http://localhost"),
      method: init.method ?? "GET",
      headers: new Headers(init.headers),
      body: typeof rawBody === "string" ? (JSON.parse(rawBody) as unknown) : undefined,
    };
    requests.push(request);
    return handler(request);
  });
  vi.stubGlobal("fetch", mock);
  return { mock, requests };
}

export function jsonResponse(status: number, body: unknown, headers: Record<string, string> = {}): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json; charset=utf-8", ...headers },
  });
}

export function noContent(): Response {
  return new Response(null, { status: 204 });
}

export function textResponse(status: number, text: string, contentType = "text/html; charset=utf-8"): Response {
  return new Response(text, { status, headers: { "Content-Type": contentType } });
}

export function apiError(
  status: number,
  code: ApiErrorCode,
  message = "Error.",
  errors: Record<string, string[]> | null = null,
): ApiError {
  return { status, code, message, errors, traceId: "00-test-trace-01" };
}

export function apiErrorResponse(
  status: number,
  code: ApiErrorCode,
  message?: string,
  errors?: Record<string, string[]> | null,
): Response {
  return jsonResponse(status, apiError(status, code, message, errors ?? null));
}

/** A request path relative to /api/v1, e.g. "/tasks". */
export function apiPath(request: RecordedRequest): string {
  return request.url.pathname.replace(/^\/api\/v1/, "");
}
