import { act, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { AuthGuard } from "@/components/auth/AuthGuard";
import { listTasks } from "@/lib/api/tasks";
import { SERVER_UNAVAILABLE_MESSAGE } from "@/lib/api/client";
import { navigation } from "../../mocks/nextNavigation";
import { USER } from "../../helpers/fixtures";
import { apiErrorResponse, apiPath, installFetch, jsonResponse, textResponse } from "../../helpers/fetch";
import { renderWithAuth } from "../../helpers/render";

describe("AuthGuard", () => {
  it("renders children once GET /auth/me succeeds", async () => {
    const { requests } = installFetch(() => jsonResponse(200, USER));
    renderWithAuth(<AuthGuard>secret content</AuthGuard>);
    expect(screen.getByRole("status")).toHaveTextContent("Checking your session");
    expect(await screen.findByText("secret content")).toBeInTheDocument();
    expect(apiPath(requests[0])).toBe("/auth/me");
    expect(navigation.router.replace).not.toHaveBeenCalled();
  });

  it("redirects to /login?returnUrl=%2Ftasks without a session (AC-AUTH-12)", async () => {
    window.history.replaceState(null, "", "/tasks");
    installFetch(() => apiErrorResponse(401, "unauthorized", "Authentication is required."));
    renderWithAuth(<AuthGuard>secret content</AuthGuard>);
    await waitFor(() => expect(navigation.router.replace).toHaveBeenCalledWith("/login?returnUrl=%2Ftasks"));
    expect(screen.queryByText("secret content")).not.toBeInTheDocument();
  });

  it("keeps the query string in returnUrl", async () => {
    window.history.replaceState(null, "", "/tasks?status=done");
    installFetch(() => apiErrorResponse(401, "unauthorized"));
    renderWithAuth(<AuthGuard>x</AuthGuard>);
    await waitFor(() =>
      expect(navigation.router.replace).toHaveBeenCalledWith("/login?returnUrl=%2Ftasks%3Fstatus%3Ddone"),
    );
  });

  it("shows the retry message instead of redirecting when the server is unavailable (AC-ERR-5)", async () => {
    let attempt = 0;
    installFetch(() => {
      attempt += 1;
      return attempt === 1 ? textResponse(502, "<html>Bad gateway</html>") : jsonResponse(200, USER);
    });
    renderWithAuth(<AuthGuard>secret content</AuthGuard>);
    expect(await screen.findByText(SERVER_UNAVAILABLE_MESSAGE)).toBeInTheDocument();
    expect(navigation.router.replace).not.toHaveBeenCalled();

    await userEvent.click(screen.getByRole("button", { name: "Try again" }));
    expect(await screen.findByText("secret content")).toBeInTheDocument();
  });

  it("redirects when a later call returns 401 unauthorized (AC-ERR-4)", async () => {
    window.history.replaceState(null, "", "/account");
    installFetch((request) =>
      apiPath(request) === "/auth/me" ? jsonResponse(200, USER) : apiErrorResponse(401, "unauthorized"),
    );
    renderWithAuth(<AuthGuard>secret content</AuthGuard>, { user: USER });
    expect(screen.getByText("secret content")).toBeInTheDocument();
    await waitFor(() => expect(navigation.router.replace).not.toHaveBeenCalled());

    await act(async () => {
      await listTasks().catch(() => undefined);
    });
    await waitFor(() => expect(navigation.router.replace).toHaveBeenCalledWith("/login?returnUrl=%2Faccount"));
  });
});
