import { expect, test } from "@playwright/test";
import {
  createTaskViaApi,
  getTaskViaApi,
  registerViaApi,
  taskRow,
  toUtcWholeSeconds,
} from "./support/api";

const isTasksCollection = (url: string): boolean => new URL(url).pathname === "/api/v1/tasks";

test.describe("Due dates and overdue", () => {
  test("AC-DUE-8: the due date input uses local time, sends the matching UTC Z string, and rejects a past time", async ({ page }) => {
    await registerViaApi(page.request);
    const posts: string[] = [];
    page.on("request", (r) => {
      if (r.method() === "POST" && isTasksCollection(r.url())) posts.push(r.url());
    });
    await page.goto("/tasks/new");
    await page.getByLabel("Title", { exact: true }).fill("Due in local time");
    const due = page.getByLabel("Due date and time");
    await expect(page.getByText(/In your local time \(Asia\/(Kolkata|Calcutta)\)/)).toBeVisible();

    // A past time: inline error, no request.
    await due.fill("2020-01-15T10:00");
    await page.getByRole("button", { name: "Create task" }).click();
    await expect(page.getByText("Due date must be in the future.")).toBeVisible();
    await expect(due).toHaveAttribute("aria-invalid", "true");
    expect(posts).toEqual([]);

    // 10:15 local in Asia/Kolkata (UTC+05:30) is 04:45 UTC.
    await due.fill("2031-03-04T10:15");
    const postRequest = page.waitForRequest((r) => r.method() === "POST" && isTasksCollection(r.url()));
    await page.getByRole("button", { name: "Create task" }).click();
    const request = await postRequest;
    expect(request.postDataJSON()).toMatchObject({ title: "Due in local time", dueDate: "2031-03-04T04:45:00Z" });

    await expect(page).toHaveURL(/\/tasks$/);
    const time = taskRow(page, "Due in local time").locator("time");
    await expect(time).toHaveAttribute("datetime", "2031-03-04T04:45:00Z");
    await expect(time).toHaveText(/Mar 4, 2031, 10:15\sAM/);
  });

  test("AC-LIST-9: a past-due task that isn't done shows \"Overdue\"; the same task marked done does not", async ({ page }) => {
    await registerViaApi(page.request);
    // The API only accepts future due dates, so create them a little ahead and move the browser clock past them.
    const dueDate = toUtcWholeSeconds(new Date(Date.now() + 10 * 60_000));
    await createTaskViaApi(page.request, { title: "Overdue open task", dueDate });
    await createTaskViaApi(page.request, { title: "Past due but done", dueDate, status: "done" });
    await page.clock.setFixedTime(new Date(Date.parse(dueDate) + 60 * 60_000));

    await page.goto("/tasks");
    const openRow = taskRow(page, "Overdue open task");
    const doneRow = taskRow(page, "Past due but done");
    await expect(openRow.getByText("Overdue", { exact: true })).toBeVisible();
    await expect(doneRow.locator("time")).toHaveAttribute("datetime", dueDate);
    await expect(doneRow.getByText("Overdue", { exact: true })).toHaveCount(0);

    await openRow.getByLabel("Status for Overdue open task").selectOption("done");
    await expect(openRow.getByText("Status: Done")).toBeVisible();
    await expect(openRow.getByText("Overdue", { exact: true })).toHaveCount(0);
  });

  test("AC-DUE-9: changing the status of an overdue task from the list works (no due-date error)", async ({ page }) => {
    await registerViaApi(page.request);
    // Due 2-3 s from now (whole seconds), then wait until it is really in the past, so the server sees an
    // overdue task too (its clock is the same machine clock). The PUT must re-send the stored due date unchanged.
    const dueMs = Math.ceil((Date.now() + 2_000) / 1000) * 1000;
    const dueDate = toUtcWholeSeconds(new Date(dueMs));
    const task = await createTaskViaApi(page.request, {
      title: "Truly overdue",
      description: "keep me",
      priority: "low",
      dueDate,
    });
    await expect.poll(() => Date.now(), { timeout: 15_000 }).toBeGreaterThan(dueMs + 1_000);

    await page.goto("/tasks");
    const row = taskRow(page, "Truly overdue");
    await expect(row.getByText("Overdue", { exact: true })).toBeVisible();

    const putResponse = page.waitForResponse(
      (r) => r.request().method() === "PUT" && new URL(r.url()).pathname === `/api/v1/tasks/${task.id}`,
    );
    await row.getByLabel("Status for Truly overdue").selectOption("inProgress");
    const response = await putResponse;

    expect(response.status()).toBe(200);
    expect(response.request().postDataJSON()).toEqual({
      title: "Truly overdue",
      description: "keep me",
      status: "inProgress",
      priority: "low",
      dueDate,
    });
    await expect(row.getByText("Status: In progress")).toBeVisible();
    await expect(row.getByText("Overdue", { exact: true })).toBeVisible(); // still overdue: not done
    await expect(row.getByRole("alert")).toHaveCount(0);
    expect(await getTaskViaApi(page.request, task.id)).toMatchObject({ status: "inProgress", dueDate });
  });
});
