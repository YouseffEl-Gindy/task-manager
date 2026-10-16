import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { LoginForm } from "@/components/auth/LoginForm";
import { SERVER_UNAVAILABLE_MESSAGE } from "@/lib/api/client";
import { navigation, setSearchParams } from "../../mocks/nextNavigation";
import { USER } from "../../helpers/fixtures";
import { apiErrorResponse, installFetch, jsonResponse, textResponse } from "../../helpers/fetch";
import { renderWithAuth } from "../../helpers/render";

async function fillAndSubmit(email: string, password: string) {
  const user = userEvent.setup();
  await user.type(screen.getByLabelText("Email"), email);
  await user.type(screen.getByLabelText("Password"), password);
  await user.click(screen.getByRole("button", { name: "Log in" }));
}

describe("LoginForm", () => {
  it("logs in and goes to /tasks by default (AC-AUTH-11)", async () => {
    const { requests } = installFetch(() => jsonResponse(200, USER));
    renderWithAuth(<LoginForm />);
    await fillAndSubmit(" ada@example.com ", "correcthorse1");
    await waitFor(() => expect(navigation.router.replace).toHaveBeenCalledWith("/tasks"));
    expect(requests[0].body).toEqual({ email: "ada@example.com", password: "correcthorse1" });
    expect(requests[0].headers.get("X-CSRF-Protection")).toBe("1");
  });

  it("returns to a safe returnUrl (AC-AUTH-12)", async () => {
    setSearchParams("returnUrl=%2Ftasks%3Fstatus%3Ddone");
    installFetch(() => jsonResponse(200, USER));
    renderWithAuth(<LoginForm />);
    await fillAndSubmit("ada@example.com", "correcthorse1");
    await waitFor(() => expect(navigation.router.replace).toHaveBeenCalledWith("/tasks?status=done"));
  });

  it("ignores a returnUrl of //evil.com (AC-AUTH-12)", async () => {
    setSearchParams("returnUrl=%2F%2Fevil.com");
    installFetch(() => jsonResponse(200, USER));
    renderWithAuth(<LoginForm />);
    await fillAndSubmit("ada@example.com", "correcthorse1");
    await waitFor(() => expect(navigation.router.replace).toHaveBeenCalledWith("/tasks"));
  });

  it("shows invalid_credentials on the form and does not redirect (AC-ERR-4)", async () => {
    installFetch(() => apiErrorResponse(401, "invalid_credentials", "Email or password is incorrect."));
    renderWithAuth(<LoginForm />);
    await fillAndSubmit("ada@example.com", "wrongpass1");
    expect(await screen.findByRole("alert")).toHaveTextContent("Email or password is incorrect.");
    expect(navigation.router.replace).not.toHaveBeenCalled();
    expect(navigation.router.push).not.toHaveBeenCalled();
  });

  it("validates required fields without calling the API", async () => {
    const { mock } = installFetch(() => jsonResponse(200, USER));
    renderWithAuth(<LoginForm />);
    await userEvent.click(screen.getByRole("button", { name: "Log in" }));
    expect(screen.getByText("Email is required.")).toBeInTheDocument();
    expect(screen.getByText("Password is required.")).toBeInTheDocument();
    expect(screen.getByLabelText("Email")).toHaveAttribute("aria-invalid", "true");
    expect(mock).not.toHaveBeenCalled();
  });

  it("shows the retry message on a proxy 504 (AC-ERR-5)", async () => {
    installFetch(() => textResponse(504, "Gateway Timeout", "text/plain"));
    renderWithAuth(<LoginForm />);
    await fillAndSubmit("ada@example.com", "correcthorse1");
    expect(await screen.findByRole("alert")).toHaveTextContent(SERVER_UNAVAILABLE_MESSAGE);
    expect(navigation.router.replace).not.toHaveBeenCalled();
  });

  it("links to /register and has no forgot-password link", () => {
    renderWithAuth(<LoginForm />);
    expect(screen.getByRole("link", { name: "Create one" })).toHaveAttribute("href", "/register");
    expect(screen.queryByText(/forgot/i)).not.toBeInTheDocument();
  });
});
