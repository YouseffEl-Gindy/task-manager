import { expect, test } from "@playwright/test";
import { BACKEND_ORIGIN, BACKEND_PORT, FRONTEND_ORIGIN } from "./support/env";
import { PASSWORD, taskRow, uniqueEmail } from "./support/api";

test("AC-PROXY-1: the browser never calls the backend origin and tm_session belongs to the frontend origin", async ({ page, context }) => {
  const requestUrls: string[] = [];
  context.on("request", (r) => requestUrls.push(r.url()));

  // Register through the UI so the session cookie is set by a real browser request.
  await page.goto("/register");
  await page.getByLabel("Display name").fill("Proxy Checker");
  await page.getByLabel("Email").fill(uniqueEmail("proxy"));
  await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
  await page.getByLabel("Confirm password", { exact: true }).fill(PASSWORD);
  const registerResponse = page.waitForResponse((r) => new URL(r.url()).pathname === "/api/v1/auth/register");
  await page.getByRole("button", { name: "Create account" }).click();
  const response = await registerResponse;

  expect(response.status()).toBe(201);
  expect(new URL(response.url()).origin).toBe(FRONTEND_ORIGIN);
  const setCookie = (await response.headerValue("set-cookie")) ?? "";
  expect(setCookie).toMatch(/(^|\n)tm_session=/);
  expect(setCookie.toLowerCase()).not.toContain("domain=");

  // Exercise GET, POST, PUT and DELETE through the UI.
  await expect(page).toHaveURL(/\/tasks$/);
  await page.getByRole("link", { name: "New task" }).click();
  await page.getByLabel("Title", { exact: true }).fill("Proxied task");
  await page.getByRole("button", { name: "Create task" }).click();
  await expect(page).toHaveURL(/\/tasks$/);
  const row = taskRow(page, "Proxied task");
  await row.getByLabel("Status for Proxied task").selectOption("inProgress");
  await expect(row.getByText("Status: In progress")).toBeVisible();
  await row.getByRole("link", { name: "Proxied task" }).click();
  await page.getByRole("button", { name: "Delete task" }).click();
  await page.getByRole("alertdialog").getByRole("button", { name: "Delete task" }).click();
  await expect(page.getByText("You have no tasks yet")).toBeVisible();

  // The session cookie: host-only on the frontend host, HttpOnly, Lax, browser-session, invisible to scripts.
  const session = (await context.cookies(FRONTEND_ORIGIN)).find((c) => c.name === "tm_session");
  expect(session).toBeDefined();
  expect(session).toMatchObject({ domain: "localhost", path: "/", httpOnly: true, sameSite: "Lax", expires: -1 });
  expect(await page.evaluate(() => document.cookie)).not.toContain("tm_session");

  await page.getByRole("button", { name: "Log out" }).click();
  await expect(page).toHaveURL(/\/login$/);

  // Every browser request went to the frontend origin; none to the backend.
  const httpRequests = requestUrls.filter((u) => u.startsWith("http"));
  const apiRequests = httpRequests.filter((u) => new URL(u).pathname.startsWith("/api/v1/"));
  expect(apiRequests.length).toBeGreaterThan(5);
  expect(httpRequests.filter((u) => new URL(u).origin !== FRONTEND_ORIGIN)).toEqual([]);
  expect(httpRequests.filter((u) => u.startsWith(BACKEND_ORIGIN) || new URL(u).port === String(BACKEND_PORT))).toEqual([]);
});
