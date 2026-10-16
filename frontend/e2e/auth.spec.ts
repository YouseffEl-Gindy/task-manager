import { expect, test, type Page } from "@playwright/test";
import { PASSWORD, registerViaApi, uniqueEmail } from "./support/api";

async function fillLoginForm(page: Page, email: string, password: string): Promise<void> {
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Log in", exact: true }).click();
}

test.describe("Auth and sessions", () => {
  test("AC-AUTH-11: after registering, the user lands on /tasks and the header shows their display name", async ({ page }) => {
    await page.goto("/register");
    await page.getByLabel("Display name").fill("  Grace Hopper  ");
    await page.getByLabel("Email").fill(uniqueEmail("register"));
    await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
    await page.getByLabel("Confirm password", { exact: true }).fill(PASSWORD);
    await page.getByRole("button", { name: "Create account" }).click();

    await expect(page).toHaveURL(/\/tasks$/);
    await expect(page.getByRole("heading", { name: "Tasks", level: 1 })).toBeVisible();
    await expect(page.getByTestId("current-user-name")).toHaveText("Grace Hopper");
  });

  test("AC-AUTH-11: after logging in, the user lands on /tasks and the header shows their display name", async ({ page, request }) => {
    const account = await registerViaApi(request, "Ada Login");

    await page.goto("/login");
    await fillLoginForm(page, account.email, account.password);

    await expect(page).toHaveURL(/\/tasks$/);
    await expect(page.getByTestId("current-user-name")).toHaveText("Ada Login");
  });

  test("AC-AUTH-12: /tasks without a session redirects to /login?returnUrl=%2Ftasks and login returns there", async ({ page, request }) => {
    const account = await registerViaApi(request);

    await page.goto("/tasks");
    await expect(page).toHaveURL(/\/login\?returnUrl=%2Ftasks$/);
    await expect(page.getByRole("heading", { name: "Log in" })).toBeVisible();

    await fillLoginForm(page, account.email, account.password);
    await expect(page).toHaveURL(/\/tasks$/);
    await expect(page.getByRole("heading", { name: "Tasks", level: 1 })).toBeVisible();
  });

  for (const returnUrl of ["//evil.com", "%2F%2Fevil.com"]) {
    test(`AC-AUTH-12: a returnUrl of //evil.com is ignored (sent as "${returnUrl}")`, async ({ page, request }) => {
      const account = await registerViaApi(request);
      const foreignRequests: string[] = [];
      page.on("request", (r) => {
        if (new URL(r.url()).hostname.includes("evil.com")) foreignRequests.push(r.url());
      });

      await page.goto(`/login?returnUrl=${returnUrl}`);
      await fillLoginForm(page, account.email, account.password);

      await expect(page).toHaveURL(/^http:\/\/localhost:3100\/tasks$/);
      await expect(page.getByRole("heading", { name: "Tasks", level: 1 })).toBeVisible();
      expect(foreignRequests).toEqual([]);
    });
  }

  test("AC-AUTH-13: logout goes to /login, and browser back to /tasks redirects to login again", async ({ page }) => {
    await registerViaApi(page.request);
    await page.goto("/tasks");
    await expect(page.getByRole("heading", { name: "Tasks", level: 1 })).toBeVisible();
    await page.getByRole("link", { name: "Account" }).click();
    await expect(page).toHaveURL(/\/account$/);

    await page.getByRole("button", { name: "Log out" }).click();
    await expect(page).toHaveURL(/\/login$/);
    expect((await page.request.get("/api/v1/auth/me")).status()).toBe(401);

    // History is now [/tasks, /login]; going back must not show the tasks page.
    await page.goBack();
    await expect(page).toHaveURL(/\/login(\?returnUrl=%2Ftasks)?$/);
    await expect(page.getByRole("heading", { name: "Log in" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Log out" })).toHaveCount(0);
  });

  test("AC-AUTH-14: the register form shows client-side errors without calling the API", async ({ page }) => {
    const registerCalls: string[] = [];
    page.on("request", (r) => {
      if (new URL(r.url()).pathname.startsWith("/api/v1/auth/register")) registerCalls.push(r.url());
    });
    await page.goto("/register");
    const displayName = page.getByLabel("Display name");
    const email = page.getByLabel("Email");
    const password = page.getByLabel("Password", { exact: true });
    const confirm = page.getByLabel("Confirm password", { exact: true });
    const submit = page.getByRole("button", { name: "Create account" });

    // Blank name, invalid email, too-short password, mismatched confirmation.
    await displayName.fill("   ");
    await email.fill("not-an-email");
    await password.fill("short1");
    await confirm.fill("different1");
    await submit.click();

    await expect(page.getByText("Display name is required.")).toBeVisible();
    await expect(page.getByText("Enter a valid email address.")).toBeVisible();
    await expect(page.getByText("Password must be at least 8 characters.")).toBeVisible();
    await expect(page.getByText("Passwords do not match.")).toBeVisible();
    for (const field of [displayName, email, password, confirm]) {
      await expect(field).toHaveAttribute("aria-invalid", "true");
    }

    // A long password without a digit is still weak.
    await displayName.fill("Valid Name");
    await email.fill(uniqueEmail("weak"));
    await password.fill("onlyletters");
    await confirm.fill("onlyletters");
    await submit.click();

    await expect(page.getByText("Password must contain at least one letter and one digit.")).toBeVisible();
    await expect(page.getByText("Display name is required.")).toHaveCount(0);
    await expect(page).toHaveURL(/\/register$/);
    expect(registerCalls).toEqual([]);
  });
});
