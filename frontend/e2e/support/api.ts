import { randomUUID } from "node:crypto";
import { expect, type APIRequestContext, type Locator, type Page } from "@playwright/test";
import type { CreateTaskRequest, TaskDto, UserDto } from "@/types/api";

/** The CSRF header every unsafe /api request needs (API.md 2). */
export const CSRF_HEADERS: Record<string, string> = { "X-CSRF-Protection": "1" };

/** Test-only password (meets the rules: 8+ chars, a letter and a digit). */
export const PASSWORD = "Password1";

export const SERVER_UNAVAILABLE_MESSAGE = "The server is starting up or unavailable. Please try again in a moment.";

export interface TestAccount {
  email: string;
  password: string;
  displayName: string;
  user: UserDto;
}

export function uniqueEmail(prefix = "e2e"): string {
  return `${prefix}-${randomUUID()}@example.com`;
}

/**
 * Registers a new user through the proxied API (relative URL on the frontend origin).
 * Pass `page.request` to log the page's browser context in (it shares the cookie jar),
 * or the `request` fixture to create an account without logging the page in.
 */
export async function registerViaApi(request: APIRequestContext, displayName = "E2E User"): Promise<TestAccount> {
  const email = uniqueEmail();
  const response = await request.post("/api/v1/auth/register", {
    headers: CSRF_HEADERS,
    data: { displayName, email, password: PASSWORD },
  });
  expect(response.status(), await response.text()).toBe(201);
  const user = (await response.json()) as UserDto;
  return { email, password: PASSWORD, displayName, user };
}

export async function createTaskViaApi(request: APIRequestContext, body: CreateTaskRequest): Promise<TaskDto> {
  const response = await request.post("/api/v1/tasks", { headers: CSRF_HEADERS, data: body });
  expect(response.status(), await response.text()).toBe(201);
  return (await response.json()) as TaskDto;
}

export async function getTaskViaApi(request: APIRequestContext, id: string): Promise<TaskDto> {
  const response = await request.get(`/api/v1/tasks/${id}`);
  expect(response.status(), await response.text()).toBe(200);
  return (await response.json()) as TaskDto;
}

/** A UTC ISO string in whole seconds ("...Z"), the precision the API stores. */
export function toUtcWholeSeconds(date: Date): string {
  return new Date(Math.floor(date.getTime() / 1000) * 1000).toISOString().replace(/\.\d{3}Z$/, "Z");
}

/** The list row (<li>) whose title link is exactly `title`. */
export function taskRow(page: Page, title: string): Locator {
  return page.getByRole("listitem").filter({ has: page.getByRole("link", { name: title, exact: true }) });
}

/** The title links of the task list, in display order. */
export function taskTitles(page: Page): Locator {
  return page.getByRole("list", { name: "Tasks" }).getByRole("link");
}

/** Sets a marker on <html>; it only survives if the page is NOT reloaded. */
export async function markDocument(page: Page): Promise<void> {
  await page.evaluate(() => {
    document.documentElement.dataset.e2eNoReload = "1";
  });
}

export async function expectNoReloadSinceMark(page: Page): Promise<void> {
  await expect(page.locator("html")).toHaveAttribute("data-e2e-no-reload", "1");
}
