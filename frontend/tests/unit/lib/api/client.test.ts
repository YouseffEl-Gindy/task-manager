import { describe, expect, it, vi } from "vitest";
import {
  ApiResponseError,
  SERVER_UNAVAILABLE_MESSAGE,
  ServerUnavailableError,
  getErrorMessage,
  getFieldErrors,
  isApiError,
  onUnauthorized,
  requestJson,
  requestVoid,
} from "@/lib/api/client";
import { apiError, apiErrorResponse, installFetch, jsonResponse, noContent, textResponse } from "../../helpers/fetch";

describe("api client: requests", () => {
  it("calls the relative /api/v1 base on the same origin with credentials", async () => {
    const { requests, mock } = installFetch(() => jsonResponse(200, { ok: true }));
    await requestJson("/auth/me");
    expect(requests[0].url.pathname).toBe("/api/v1/auth/me");
    const [input, init] = mock.mock.calls[0];
    expect(String(input)).toBe("/api/v1/auth/me");
    expect(init?.credentials).toBe("same-origin");
  });

  it("does not send the CSRF header on GET", async () => {
    const { requests } = installFetch(() => jsonResponse(200, []));
    await requestJson("/tasks");
    expect(requests[0].method).toBe("GET");
    expect(requests[0].headers.has("X-CSRF-Protection")).toBe(false);
  });

  it.each(["POST", "PUT", "PATCH", "DELETE"] as const)("sends X-CSRF-Protection: 1 on %s", async (method) => {
    const { requests } = installFetch(() => noContent());
    await requestVoid("/tasks/abc", { method });
    expect(requests[0].method).toBe(method);
    expect(requests[0].headers.get("X-CSRF-Protection")).toBe("1");
  });

  it("serializes a JSON body with a JSON content type", async () => {
    const { requests } = installFetch(() => jsonResponse(201, { id: "1" }));
    await requestJson("/tasks", { method: "POST", body: { title: "Buy milk" } });
    expect(requests[0].headers.get("Content-Type")).toBe("application/json");
    expect(requests[0].body).toEqual({ title: "Buy milk" });
  });

  it("appends query parameters", async () => {
    const { requests } = installFetch(() => jsonResponse(200, []));
    await requestJson("/tasks", { query: new URLSearchParams({ status: "done", search: "a b" }) });
    expect(requests[0].url.pathname).toBe("/api/v1/tasks");
    expect(requests[0].url.searchParams.get("status")).toBe("done");
    expect(requests[0].url.searchParams.get("search")).toBe("a b");
  });

  it("returns undefined for 204 No Content", async () => {
    installFetch(() => noContent());
    await expect(requestVoid("/auth/logout", { method: "POST" })).resolves.toBeUndefined();
  });
});

describe("api client: ApiError responses", () => {
  it("throws ApiResponseError with code, status and field errors", async () => {
    installFetch(() =>
      apiErrorResponse(400, "validation_failed", "One or more fields are invalid.", {
        title: ["Title is required."],
        dueDate: ["Due date must be in the future."],
      }),
    );
    const error = await requestJson("/tasks", { method: "POST", body: {} }).catch((e: unknown) => e);
    expect(error).toBeInstanceOf(ApiResponseError);
    const apiErr = error as ApiResponseError;
    expect(apiErr.status).toBe(400);
    expect(apiErr.code).toBe("validation_failed");
    expect(apiErr.message).toBe("One or more fields are invalid.");
    expect(getFieldErrors(apiErr)).toEqual({ title: "Title is required.", dueDate: "Due date must be in the future." });
  });

  it("notifies unauthorized listeners on 401 unauthorized", async () => {
    installFetch(() => apiErrorResponse(401, "unauthorized", "Authentication is required."));
    const listener = vi.fn();
    const unsubscribe = onUnauthorized(listener);
    await expect(requestJson("/tasks")).rejects.toBeInstanceOf(ApiResponseError);
    expect(listener).toHaveBeenCalledTimes(1);
    unsubscribe();
    await expect(requestJson("/tasks")).rejects.toBeInstanceOf(ApiResponseError);
    expect(listener).toHaveBeenCalledTimes(1);
  });

  it("does not notify on 401 invalid_credentials (login form error)", async () => {
    installFetch(() => apiErrorResponse(401, "invalid_credentials", "Email or password is incorrect."));
    const listener = vi.fn();
    const unsubscribe = onUnauthorized(listener);
    const error = await requestJson("/auth/login", { method: "POST", body: {} }).catch((e: unknown) => e);
    unsubscribe();
    expect(listener).not.toHaveBeenCalled();
    expect((error as ApiResponseError).code).toBe("invalid_credentials");
  });

  it("parses 403 csrf_failed and 429 rate_limited as ApiResponseError", async () => {
    installFetch(() => apiErrorResponse(403, "csrf_failed"));
    await expect(requestVoid("/tasks/x", { method: "DELETE" })).rejects.toMatchObject({ code: "csrf_failed" });
    installFetch(() => apiErrorResponse(429, "rate_limited", "Too many requests. Please try again later."));
    const error = await requestJson("/auth/login", { method: "POST", body: {} }).catch((e: unknown) => e);
    expect(getErrorMessage(error)).toBe("Too many requests. Please try again later.");
  });
});

describe("api client: non-ApiError responses (AC-ERR-5)", () => {
  it.each([502, 503, 504])("treats a %i HTML page as server unavailable", async (status) => {
    installFetch(() => textResponse(status, "<html><body>Bad Gateway</body></html>"));
    const error = await requestJson("/tasks").catch((e: unknown) => e);
    expect(error).toBeInstanceOf(ServerUnavailableError);
    expect((error as ServerUnavailableError).status).toBe(status);
    expect(getErrorMessage(error)).toBe(SERVER_UNAVAILABLE_MESSAGE);
  });

  it("treats the proxy's plain-text 500 (backend unreachable in local dev) as server unavailable", async () => {
    // Observed in the smoke test: with the backend stopped, `next dev` answers 500 "Internal Server Error" (no JSON).
    installFetch(() => new Response("Internal Server Error", { status: 500 }));
    const error = await requestJson("/auth/me").catch((e: unknown) => e);
    expect(error).toBeInstanceOf(ServerUnavailableError);
    expect(getErrorMessage(error)).toBe(SERVER_UNAVAILABLE_MESSAGE);
  });

  it("treats an empty 504 body as server unavailable", async () => {
    installFetch(() => new Response(null, { status: 504 }));
    await expect(requestJson("/tasks")).rejects.toBeInstanceOf(ServerUnavailableError);
  });

  it("treats a JSON content type with an invalid body as server unavailable", async () => {
    installFetch(() => textResponse(502, "{not json", "application/json"));
    await expect(requestJson("/tasks")).rejects.toBeInstanceOf(ServerUnavailableError);
  });

  it("treats JSON that isn't an ApiError as server unavailable", async () => {
    installFetch(() => jsonResponse(500, { error: "boom" }));
    await expect(requestJson("/tasks")).rejects.toBeInstanceOf(ServerUnavailableError);
  });

  it("treats a 401 without an ApiError body as unavailable and does not notify", async () => {
    installFetch(() => textResponse(401, "Unauthorized", "text/plain"));
    const listener = vi.fn();
    const unsubscribe = onUnauthorized(listener);
    await expect(requestJson("/tasks")).rejects.toBeInstanceOf(ServerUnavailableError);
    unsubscribe();
    expect(listener).not.toHaveBeenCalled();
  });

  it("treats a network failure as server unavailable with no status", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("Failed to fetch")));
    const error = await requestJson("/tasks").catch((e: unknown) => e);
    expect(error).toBeInstanceOf(ServerUnavailableError);
    expect((error as ServerUnavailableError).status).toBeNull();
  });

  it("treats a 2xx HTML page (e.g. a quota page) as server unavailable when JSON is expected", async () => {
    installFetch(() => textResponse(200, "<html>Site stopped</html>"));
    await expect(requestJson("/tasks")).rejects.toBeInstanceOf(ServerUnavailableError);
  });

  it("rethrows aborts unchanged", async () => {
    const abort = new DOMException("The operation was aborted.", "AbortError");
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(abort));
    await expect(requestJson("/tasks")).rejects.toBe(abort);
  });
});

describe("isApiError", () => {
  it("accepts a full ApiError and rejects partial or unknown shapes", () => {
    expect(isApiError(apiError(404, "not_found"))).toBe(true);
    expect(isApiError({ ...apiError(400, "validation_failed"), errors: { title: ["x"] } })).toBe(true);
    expect(isApiError({ status: 404, code: "not_found", message: "x" })).toBe(false);
    expect(isApiError({ ...apiError(400, "validation_failed"), code: "something_else" })).toBe(false);
    expect(isApiError({ ...apiError(400, "validation_failed"), errors: { title: "x" } })).toBe(false);
    expect(isApiError("oops")).toBe(false);
    expect(isApiError(null)).toBe(false);
  });
});
