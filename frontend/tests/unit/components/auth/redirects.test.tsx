import { screen, waitFor } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { HomeRedirect } from "@/components/auth/HomeRedirect";
import { RedirectIfAuthenticated } from "@/components/auth/RedirectIfAuthenticated";
import { SERVER_UNAVAILABLE_MESSAGE } from "@/lib/api/client";
import { navigation } from "../../mocks/nextNavigation";
import { USER } from "../../helpers/fixtures";
import { apiErrorResponse, installFetch, jsonResponse } from "../../helpers/fetch";
import { renderWithAuth } from "../../helpers/render";

describe("HomeRedirect (/)", () => {
  it("goes to /tasks with a session", async () => {
    installFetch(() => jsonResponse(200, USER));
    renderWithAuth(<HomeRedirect />);
    await waitFor(() => expect(navigation.router.replace).toHaveBeenCalledWith("/tasks"));
  });

  it("goes to /login without a session", async () => {
    installFetch(() => apiErrorResponse(401, "unauthorized"));
    renderWithAuth(<HomeRedirect />);
    await waitFor(() => expect(navigation.router.replace).toHaveBeenCalledWith("/login"));
  });

  it("shows the retry message when the server is unavailable", async () => {
    installFetch(() => new Response("<html></html>", { status: 503, headers: { "Content-Type": "text/html" } }));
    renderWithAuth(<HomeRedirect />);
    expect(await screen.findByText(SERVER_UNAVAILABLE_MESSAGE)).toBeInTheDocument();
    expect(navigation.router.replace).not.toHaveBeenCalled();
  });
});

describe("RedirectIfAuthenticated (/login, /register)", () => {
  it("sends logged-in users to /tasks", async () => {
    installFetch(() => jsonResponse(200, USER));
    renderWithAuth(<RedirectIfAuthenticated />);
    await waitFor(() => expect(navigation.router.replace).toHaveBeenCalledWith("/tasks"));
  });

  it("does nothing without a session", async () => {
    const { mock } = installFetch(() => apiErrorResponse(401, "unauthorized"));
    renderWithAuth(<RedirectIfAuthenticated />);
    await waitFor(() => expect(mock).toHaveBeenCalled());
    expect(navigation.router.replace).not.toHaveBeenCalled();
  });
});
