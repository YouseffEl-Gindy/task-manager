import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { ChangePasswordForm } from "@/components/account/ChangePasswordForm";
import { DeleteAccountSection } from "@/components/account/DeleteAccountSection";
import { ProfileForm } from "@/components/account/ProfileForm";
import { AppHeader } from "@/components/layout/AppHeader";
import { navigation } from "../../mocks/nextNavigation";
import { USER } from "../../helpers/fixtures";
import { apiErrorResponse, apiPath, installFetch, jsonResponse, noContent } from "../../helpers/fetch";
import { renderWithAuth } from "../../helpers/render";

describe("ProfileForm (AC-ACCT-UI-1)", () => {
  it("updates the display name and the header without a reload", async () => {
    const { requests } = installFetch(() => jsonResponse(200, { ...USER, displayName: "Ada L." }));
    renderWithAuth(
      <>
        <AppHeader />
        <ProfileForm />
      </>,
      { user: USER },
    );
    const input = screen.getByLabelText("Display name");
    expect(input).toHaveValue("Ada Lovelace");
    await userEvent.clear(input);
    await userEvent.type(input, "  Ada L.  ");
    await userEvent.click(screen.getByRole("button", { name: "Save display name" }));

    expect(await screen.findByText("Display name updated.")).toBeInTheDocument();
    expect(screen.getByTestId("current-user-name")).toHaveTextContent("Ada L.");
    expect(requests[0]).toMatchObject({ method: "PUT", body: { displayName: "Ada L." } });
    expect(apiPath(requests[0])).toBe("/account/profile");
  });

  it("validates a blank name on the client", async () => {
    const { mock } = installFetch(() => jsonResponse(200, USER));
    renderWithAuth(<ProfileForm />, { user: USER });
    await userEvent.clear(screen.getByLabelText("Display name"));
    await userEvent.click(screen.getByRole("button", { name: "Save display name" }));
    expect(screen.getByText("Display name is required.")).toBeInTheDocument();
    expect(mock).not.toHaveBeenCalled();
  });
});

describe("ChangePasswordForm (AC-ACCT-UI-2)", () => {
  async function submit(current: string, next: string, confirm = next) {
    const user = userEvent.setup();
    await user.type(screen.getByLabelText("Current password"), current);
    await user.type(screen.getByLabelText("New password"), next);
    await user.type(screen.getByLabelText("Confirm new password"), confirm);
    await user.click(screen.getByRole("button", { name: "Change password" }));
  }

  it("shows an inline error for a wrong current password and stays logged in", async () => {
    installFetch(() =>
      apiErrorResponse(400, "validation_failed", "One or more fields are invalid.", {
        currentPassword: ["Current password is incorrect."],
      }),
    );
    renderWithAuth(<ChangePasswordForm />, { user: USER });
    await submit("wrongpass1", "batterystaple2");
    expect(await screen.findByText("Current password is incorrect.")).toBeInTheDocument();
    expect(screen.getByLabelText("Current password")).toHaveAttribute("aria-invalid", "true");
    expect(navigation.router.replace).not.toHaveBeenCalled();
  });

  it("shows a success notice and clears the fields", async () => {
    const { requests } = installFetch(() => noContent());
    renderWithAuth(<ChangePasswordForm />, { user: USER });
    await submit("correcthorse1", "batterystaple2");
    expect(await screen.findByText("Your password has been changed.")).toBeInTheDocument();
    expect(screen.getByLabelText("Current password")).toHaveValue("");
    expect(screen.getByLabelText("New password")).toHaveValue("");
    expect(screen.getByLabelText("Confirm new password")).toHaveValue("");
    expect(requests[0].body).toEqual({ currentPassword: "correcthorse1", newPassword: "batterystaple2" });
  });

  it("checks the rules on the client", async () => {
    const { mock } = installFetch(() => noContent());
    renderWithAuth(<ChangePasswordForm />, { user: USER });
    await submit("correcthorse1", "correcthorse1");
    expect(screen.getByText("The new password must be different from the current password.")).toBeInTheDocument();
    expect(mock).not.toHaveBeenCalled();
  });
});

describe("DeleteAccountSection (AC-ACCT-UI-3)", () => {
  it("requires the password before asking for confirmation", async () => {
    const { mock } = installFetch(() => noContent());
    renderWithAuth(<DeleteAccountSection />, { user: USER });
    await userEvent.click(screen.getByRole("button", { name: "Delete my account" }));
    expect(screen.getByText("Password is required.")).toBeInTheDocument();
    expect(screen.queryByRole("alertdialog")).not.toBeInTheDocument();
    expect(mock).not.toHaveBeenCalled();
  });

  it("does nothing on cancel", async () => {
    const { mock } = installFetch(() => noContent());
    renderWithAuth(<DeleteAccountSection />, { user: USER });
    await userEvent.type(screen.getByLabelText("Password"), "correcthorse1");
    await userEvent.click(screen.getByRole("button", { name: "Delete my account" }));
    const dialog = screen.getByRole("alertdialog", { name: "Delete your account?" });
    await userEvent.click(within(dialog).getByRole("button", { name: "Cancel" }));
    expect(screen.queryByRole("alertdialog")).not.toBeInTheDocument();
    expect(mock).not.toHaveBeenCalled();
    expect(navigation.router.replace).not.toHaveBeenCalled();
  });

  it("deletes and goes to /login on confirm", async () => {
    const { requests } = installFetch(() => noContent());
    renderWithAuth(<DeleteAccountSection />, { user: USER });
    await userEvent.type(screen.getByLabelText("Password"), "correcthorse1");
    await userEvent.click(screen.getByRole("button", { name: "Delete my account" }));
    await userEvent.click(within(screen.getByRole("alertdialog")).getByRole("button", { name: "Delete account" }));
    await waitFor(() => expect(navigation.router.replace).toHaveBeenCalledWith("/login"));
    expect(requests[0]).toMatchObject({ method: "POST", body: { password: "correcthorse1" } });
    expect(apiPath(requests[0])).toBe("/account/delete");
    expect(requests[0].headers.get("X-CSRF-Protection")).toBe("1");
  });

  it("shows a wrong password next to the field", async () => {
    installFetch(() =>
      apiErrorResponse(400, "validation_failed", "One or more fields are invalid.", { password: ["Password is incorrect."] }),
    );
    renderWithAuth(<DeleteAccountSection />, { user: USER });
    await userEvent.type(screen.getByLabelText("Password"), "nope12345");
    await userEvent.click(screen.getByRole("button", { name: "Delete my account" }));
    await userEvent.click(within(screen.getByRole("alertdialog")).getByRole("button", { name: "Delete account" }));
    expect(await screen.findByText("Password is incorrect.")).toBeInTheDocument();
    expect(screen.queryByRole("alertdialog")).not.toBeInTheDocument();
    expect(navigation.router.replace).not.toHaveBeenCalled();
  });
});
