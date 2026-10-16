import { describe, expect, it } from "vitest";
import { ApiResponseError, SERVER_UNAVAILABLE_MESSAGE, ServerUnavailableError } from "@/lib/api/client";
import { mapServerError } from "@/lib/forms/serverErrors";
import { apiError } from "../../helpers/fetch";

describe("mapServerError", () => {
  it("maps known validation keys to fields", () => {
    const error = new ApiResponseError(apiError(400, "validation_failed", "One or more fields are invalid.", { title: ["Title is required."] }));
    expect(mapServerError(error, ["title", "dueDate"])).toEqual({
      fieldErrors: { title: "Title is required." },
      formError: null,
    });
  });

  it("puts unknown keys such as `body` in the form error", () => {
    const error = new ApiResponseError(apiError(400, "validation_failed", "One or more fields are invalid.", { body: ["Malformed JSON."] }));
    expect(mapServerError(error, ["title"])).toEqual({ fieldErrors: {}, formError: "Malformed JSON." });
  });

  it("uses the API message for other codes", () => {
    const error = new ApiResponseError(apiError(404, "not_found", "The requested resource was not found."));
    expect(mapServerError(error, ["title"])).toEqual({ fieldErrors: {}, formError: "The requested resource was not found." });
  });

  it("uses the retry message when the server is unavailable", () => {
    expect(mapServerError(new ServerUnavailableError(502), ["title"]).formError).toBe(SERVER_UNAVAILABLE_MESSAGE);
  });
});
