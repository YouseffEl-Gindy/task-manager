import { expect, test, type Page } from "@playwright/test";
import { SERVER_UNAVAILABLE_MESSAGE, registerViaApi } from "./support/api";

/** Collects uncaught page errors, so "no crash" can be asserted. */
function collectPageErrors(page: Page): Error[] {
  const errors: Error[] = [];
  page.on("pageerror", (error) => errors.push(error));
  return errors;
}

test.describe("Error handling", () => {
  test("AC-ERR-4: an unauthorized error on a protected page redirects to /login?returnUrl=...", async ({ page, context }) => {
    const account = await registerViaApi(page.request);
    await page.goto("/tasks?status=done");
    await expect(page.getByText("No tasks match your filters")).toBeVisible();

    // The session disappears; the next API call from the page gets 401 unauthorized.
    await context.clearCookies();
    await page.getByLabel("Priority", { exact: true }).selectOption("high");

    await expect(page).toHaveURL(/\/login\?returnUrl=/);
    const returnUrl = new URL(page.url()).searchParams.get("returnUrl");
    expect(returnUrl).toMatch(/^\/tasks\?status=done/);

    // Logging in again returns to that view.
    await page.getByLabel("Email").fill(account.email);
    await page.getByLabel("Password", { exact: true }).fill(account.password);
    await page.getByRole("button", { name: "Log in", exact: true }).click();
    await expect(page).toHaveURL(/\/tasks\?status=done/);
    await expect(page.getByLabel("Status", { exact: true })).toHaveValue("done");
  });

  test("AC-ERR-4: the invalid_credentials 401 on the login form is shown inline and does not redirect", async ({ page, request }) => {
    const account = await registerViaApi(request);
    await page.goto("/login?returnUrl=%2Faccount");

    await page.getByLabel("Email").fill(account.email);
    await page.getByLabel("Password", { exact: true }).fill("WrongPassword9");
    await page.getByRole("button", { name: "Log in", exact: true }).click();

    await expect(page.getByText("Email or password is incorrect.")).toBeVisible();
    await expect(page).toHaveURL(/\/login\?returnUrl=%2Faccount$/);
    await expect(page.getByLabel("Email")).toHaveValue(account.email);
  });

  for (const status of [502, 503, 504]) {
    test(`AC-ERR-5: a ${status} non-JSON response shows the "server unavailable" message, no crash, no redirect`, async ({ page }) => {
      await registerViaApi(page.request);
      const pageErrors = collectPageErrors(page);
      await page.route("**/api/v1/auth/me", (route) =>
        route.fulfill({
          status,
          contentType: "text/html; charset=utf-8",
          body: `<!DOCTYPE html><html><body><h1>${status} - upstream unavailable</h1></body></html>`,
        }),
      );

      await page.goto("/tasks");

      await expect(page.getByText(SERVER_UNAVAILABLE_MESSAGE)).toBeVisible();
      await expect(page.getByText("Can't reach the server")).toBeVisible();
      await expect(page).toHaveURL(/\/tasks$/);

      // Once the backend answers again, "Try again" recovers without a reload.
      await page.unroute("**/api/v1/auth/me");
      await page.getByRole("button", { name: "Try again" }).click();
      await expect(page.getByRole("heading", { name: "Tasks", level: 1 })).toBeVisible();
      expect(pageErrors).toEqual([]);
    });
  }

  test("AC-ERR-5: a network failure while loading the list shows the message, no crash, no redirect", async ({ page }) => {
    await registerViaApi(page.request);
    const pageErrors = collectPageErrors(page);
    await page.route((url) => url.pathname === "/api/v1/tasks", (route) => route.abort("failed"));

    await page.goto("/tasks");

    await expect(page.getByText(SERVER_UNAVAILABLE_MESSAGE)).toBeVisible();
    await expect(page).toHaveURL(/\/tasks$/);
    await expect(page.getByRole("heading", { name: "Tasks", level: 1 })).toBeVisible(); // the page itself still renders
    expect(pageErrors).toEqual([]);
  });

  test("AC-ERR-5: a 503 non-JSON response to a form submit shows the message on the form", async ({ page }) => {
    await registerViaApi(page.request);
    const pageErrors = collectPageErrors(page);
    await page.route(
      (url) => url.pathname === "/api/v1/tasks",
      (route) =>
        route.request().method() === "POST"
          ? route.fulfill({ status: 503, contentType: "text/plain", body: "Service Unavailable" })
          : route.fallback(),
    );
    await page.goto("/tasks/new");
    await page.getByLabel("Title", { exact: true }).fill("Not saved");
    await page.getByRole("button", { name: "Create task" }).click();

    await expect(page.getByText(SERVER_UNAVAILABLE_MESSAGE)).toBeVisible();
    await expect(page).toHaveURL(/\/tasks\/new$/);
    await expect(page.getByRole("button", { name: "Create task" })).toBeEnabled();
    expect(pageErrors).toEqual([]);
  });
});
