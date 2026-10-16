import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { RegisterForm } from "@/components/auth/RegisterForm";
import { navigation } from "../../mocks/nextNavigation";
import { USER } from "../../helpers/fixtures";
import { apiErrorResponse, installFetch, jsonResponse } from "../../helpers/fetch";
import { renderWithAuth } from "../../helpers/render";

interface Values {
  displayName?: string;
  email?: string;
  password?: string;
  confirm?: string;
}

async function fill({ displayName = "", email = "", password = "", confirm = "" }: Values) {
  const user = userEvent.setup();
  if (displayName) await user.type(screen.getByLabelText("Display name"), displayName);
  if (email) await user.type(screen.getByLabelText("Email"), email);
  if (password) await user.type(screen.getByLabelText("Password"), password);
  if (confirm) await user.type(screen.getByLabelText("Confirm password"), confirm);
  await user.click(screen.getByRole("button", { name: "Create account" }));
}

describe("RegisterForm", () => {
  it("shows client-side errors without calling the API (AC-AUTH-14)", async () => {
    const { mock } = installFetch(() => jsonResponse(201, USER));
    renderWithAuth(<RegisterForm />);
    await fill({ displayName: "   ", email: "not-an-email", password: "weakpass", confirm: "different1" });
    expect(screen.getByText("Display name is required.")).toBeInTheDocument();
    expect(screen.getByText("Enter a valid email address.")).toBeInTheDocument();
    expect(screen.getByText("Password must contain at least one letter and one digit.")).toBeInTheDocument();
    expect(screen.getByText("Passwords do not match.")).toBeInTheDocument();
    expect(mock).not.toHaveBeenCalled();
  });

  it("registers and goes to /tasks (AC-AUTH-11)", async () => {
    const { requests } = installFetch(() => jsonResponse(201, USER));
    renderWithAuth(<RegisterForm />);
    await fill({ displayName: " Ada Lovelace ", email: "Ada@Example.com", password: "correcthorse1", confirm: "correcthorse1" });
    await waitFor(() => expect(navigation.router.replace).toHaveBeenCalledWith("/tasks"));
    expect(requests[0].body).toEqual({ displayName: "Ada Lovelace", email: "Ada@Example.com", password: "correcthorse1" });
  });

  it("shows email_taken next to the email field", async () => {
    installFetch(() => apiErrorResponse(409, "email_taken", "An account with this email already exists."));
    renderWithAuth(<RegisterForm />);
    await fill({ displayName: "Ada", email: "ada@example.com", password: "correcthorse1", confirm: "correcthorse1" });
    expect(await screen.findByText("An account with this email already exists.")).toBeInTheDocument();
    expect(screen.getByLabelText("Email")).toHaveAttribute("aria-invalid", "true");
    expect(navigation.router.replace).not.toHaveBeenCalled();
  });

  it("shows server validation errors next to their fields", async () => {
    installFetch(() =>
      apiErrorResponse(400, "validation_failed", "One or more fields are invalid.", {
        password: ["Password is too common."],
      }),
    );
    renderWithAuth(<RegisterForm />);
    await fill({ displayName: "Ada", email: "ada@example.com", password: "correcthorse1", confirm: "correcthorse1" });
    expect(await screen.findByText("Password is too common.")).toBeInTheDocument();
    expect(screen.getByLabelText("Password")).toHaveAttribute("aria-invalid", "true");
  });
});
