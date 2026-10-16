import { expect, test } from "@playwright/test";
import {
  CSRF_HEADERS,
  createTaskViaApi,
  expectNoReloadSinceMark,
  markDocument,
  registerViaApi,
} from "./support/api";

test.describe("Account page", () => {
  test("AC-ACCT-UI-1: updating the display name changes the header without a reload", async ({ page }) => {
    await registerViaApi(page.request, "Before Rename");
    await page.goto("/account");
    const header = page.getByTestId("current-user-name");
    await expect(header).toHaveText("Before Rename");
    const field = page.getByLabel("Display name");
    await expect(field).toHaveValue("Before Rename");
    await markDocument(page);

    await field.fill("  After Rename  ");
    await page.getByRole("button", { name: "Save display name" }).click();

    await expect(page.getByText("Display name updated.")).toBeVisible();
    await expect(header).toHaveText("After Rename");
    await expect(field).toHaveValue("After Rename");
    await expectNoReloadSinceMark(page);
    const me = await page.request.get("/api/v1/auth/me");
    expect(await me.json()).toMatchObject({ displayName: "After Rename" });
  });

  test("AC-ACCT-UI-2: wrong current password shows an inline error; success shows a notice and the user stays logged in", async ({ page, request }) => {
    const account = await registerViaApi(page.request, "Password Changer");
    await page.goto("/account");
    const current = page.getByLabel("Current password", { exact: true });
    const next = page.getByLabel("New password", { exact: true });
    const confirm = page.getByLabel("Confirm new password", { exact: true });
    const submit = page.getByRole("button", { name: "Change password" });

    await current.fill("WrongPassword9");
    await next.fill("NewPassword2");
    await confirm.fill("NewPassword2");
    await submit.click();

    await expect(page.getByText("Current password is incorrect.")).toBeVisible();
    await expect(current).toHaveAttribute("aria-invalid", "true");
    await expect(page).toHaveURL(/\/account$/); // a 400, not a 401: no redirect to login

    await current.fill(account.password);
    await next.fill("NewPassword2");
    await confirm.fill("NewPassword2");
    await submit.click();

    await expect(page.getByText("Your password has been changed.")).toBeVisible();
    await expect(current).toHaveValue("");
    await expect(next).toHaveValue("");
    await expect(page.getByText("Current password is incorrect.")).toHaveCount(0);

    // Still logged in, also after a full reload.
    await page.reload();
    await expect(page).toHaveURL(/\/account$/);
    await expect(page.getByTestId("current-user-name")).toHaveText("Password Changer");

    // The new password works for a separate login; the old one doesn't.
    const login = (password: string) =>
      request.post("/api/v1/auth/login", { headers: CSRF_HEADERS, data: { email: account.email, password } });
    expect((await login(account.password)).status()).toBe(401);
    expect((await login("NewPassword2")).status()).toBe(200);
  });

  test("AC-ACCT-UI-3: deleting the account needs the password and a confirmation; cancel does nothing, confirm goes to /login", async ({ page, request }) => {
    const account = await registerViaApi(page.request);
    await createTaskViaApi(page.request, { title: "Deleted with the account" });
    await page.goto("/account");
    const form = page.getByRole("form", { name: "Delete account" });
    const deleteButton = form.getByRole("button", { name: "Delete my account" });
    const dialog = page.getByRole("alertdialog", { name: "Delete your account?" });

    // No password: inline error, no dialog.
    await deleteButton.click();
    await expect(form.getByText("Password is required.")).toBeVisible();
    await expect(dialog).toHaveCount(0);

    // Password + Cancel: nothing happens.
    await form.getByLabel("Password", { exact: true }).fill(account.password);
    await deleteButton.click();
    await expect(dialog).toBeVisible();
    await dialog.getByRole("button", { name: "Cancel" }).click();
    await expect(dialog).toHaveCount(0);
    await expect(page).toHaveURL(/\/account$/);
    expect((await page.request.get("/api/v1/auth/me")).status()).toBe(200);
    expect(await (await page.request.get("/api/v1/tasks")).json()).toHaveLength(1);

    // Password + Confirm: account gone, back to /login.
    await deleteButton.click();
    await expect(dialog).toBeVisible();
    await dialog.getByRole("button", { name: "Delete account" }).click();
    await expect(page).toHaveURL(/\/login$/);
    expect((await page.request.get("/api/v1/auth/me")).status()).toBe(401);
    const login = await request.post("/api/v1/auth/login", {
      headers: CSRF_HEADERS,
      data: { email: account.email, password: account.password },
    });
    expect(login.status()).toBe(401);
  });
});
