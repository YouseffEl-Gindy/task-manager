import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { AuthGuard } from "@/components/auth/AuthGuard";
import { AppHeader } from "@/components/layout/AppHeader";
import { SERVER_UNAVAILABLE_MESSAGE } from "@/lib/api/client";
import { navigation } from "../../mocks/nextNavigation";
import { USER } from "../../helpers/fixtures";
import { apiPath, installFetch, jsonResponse, noContent, textResponse } from "../../helpers/fetch";
import { renderWithAuth } from "../../helpers/render";

describe("AppHeader", () => {
  it("shows the display name and nav links with the current page marked", () => {
    navigation.pathname = "/tasks/123";
    renderWithAuth(<AppHeader />, { user: USER });
    expect(screen.getByText("Ada Lovelace")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Tasks" })).toHaveAttribute("aria-current", "page");
    expect(screen.getByRole("link", { name: "Account" })).not.toHaveAttribute("aria-current");
  });
});

describe("LogoutButton (AC-AUTH-13)", () => {
  it("calls POST /auth/logout with CSRF and goes to /login", async () => {
    const { requests } = installFetch((r) => (apiPath(r) === "/auth/logout" ? noContent() : jsonResponse(200, USER)));
    renderWithAuth(
      <AuthGuard>
        <AppHeader />
      </AuthGuard>,
      { user: USER },
    );
    await userEvent.click(screen.getByRole("button", { name: "Log out" }));
    await waitFor(() => expect(navigation.router.replace).toHaveBeenCalledWith("/login"));
    const logout = requests.find((r) => apiPath(r) === "/auth/logout");
    expect(logout?.method).toBe("POST");
    expect(logout?.headers.get("X-CSRF-Protection")).toBe("1");
    // The protected content is gone once signed out.
    expect(screen.queryByText("Ada Lovelace")).not.toBeInTheDocument();
    expect(navigation.router.replace).not.toHaveBeenCalledWith(expect.stringContaining("returnUrl"));
  });

  it("stays signed in and shows an error if logout fails", async () => {
    installFetch((r) => (apiPath(r) === "/auth/logout" ? textResponse(503, "Service Unavailable") : jsonResponse(200, USER)));
    renderWithAuth(<AppHeader />, { user: USER });
    await userEvent.click(screen.getByRole("button", { name: "Log out" }));
    expect(await screen.findByRole("alert")).toHaveTextContent(SERVER_UNAVAILABLE_MESSAGE);
    expect(screen.getByText("Ada Lovelace")).toBeInTheDocument();
    expect(navigation.router.replace).not.toHaveBeenCalled();
  });
});
