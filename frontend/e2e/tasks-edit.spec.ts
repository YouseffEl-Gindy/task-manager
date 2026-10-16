import { randomUUID } from "node:crypto";
import { expect, test } from "@playwright/test";
import {
  createTaskViaApi,
  expectNoReloadSinceMark,
  getTaskViaApi,
  markDocument,
  registerViaApi,
  taskRow,
} from "./support/api";

const isTasksCollection = (url: string): boolean => new URL(url).pathname === "/api/v1/tasks";

test.describe("Create, edit and delete tasks", () => {
  test("AC-CREATE-5: an empty title shows an inline error without an API call; a valid submit goes to /tasks", async ({ page }) => {
    await registerViaApi(page.request);
    const posts: string[] = [];
    page.on("request", (r) => {
      if (r.method() === "POST" && isTasksCollection(r.url())) posts.push(r.url());
    });
    await page.goto("/tasks/new");
    const title = page.getByLabel("Title", { exact: true });

    await page.getByRole("button", { name: "Create task" }).click();
    await expect(page.getByText("Title is required.")).toBeVisible();
    await expect(title).toHaveAttribute("aria-invalid", "true");
    await expect(page).toHaveURL(/\/tasks\/new$/);
    expect(posts).toEqual([]);

    await title.fill("Write the e2e tests");
    await page.getByLabel("Description (optional)").fill("with Playwright");
    await page.getByLabel("Priority").selectOption("high");
    await page.getByRole("button", { name: "Create task" }).click();

    await expect(page).toHaveURL(/\/tasks$/);
    const row = taskRow(page, "Write the e2e tests");
    await expect(row).toBeVisible();
    await expect(row.getByText("with Playwright")).toBeVisible();
    await expect(row.getByText("High priority")).toBeVisible();
    expect(posts).toHaveLength(1);
  });

  test("AC-EDIT-5: the edit page loads current values (due date in local time), saving goes to /tasks", async ({ page }) => {
    await registerViaApi(page.request);
    const task = await createTaskViaApi(page.request, {
      title: "Edit me",
      description: "Original description",
      status: "inProgress",
      priority: "high",
      dueDate: "2031-03-04T04:45:00Z", // 10:15 in Asia/Kolkata (UTC+05:30)
    });

    await page.goto(`/tasks/${task.id}`);
    await expect(page.getByRole("heading", { name: "Edit task" })).toBeVisible();
    const title = page.getByLabel("Title", { exact: true });
    await expect(title).toHaveValue("Edit me");
    await expect(page.getByLabel("Description (optional)")).toHaveValue("Original description");
    await expect(page.getByLabel("Status", { exact: true })).toHaveValue("inProgress");
    await expect(page.getByLabel("Priority", { exact: true })).toHaveValue("high");
    await expect(page.getByLabel("Due date and time")).toHaveValue("2031-03-04T10:15");

    await title.fill("Edited title");
    await page.getByRole("button", { name: "Save changes" }).click();

    await expect(page).toHaveURL(/\/tasks$/);
    await expect(taskRow(page, "Edited title")).toBeVisible();
    const saved = await getTaskViaApi(page.request, task.id);
    expect(saved).toMatchObject({
      title: "Edited title",
      description: "Original description",
      status: "inProgress",
      priority: "high",
      dueDate: "2031-03-04T04:45:00Z",
      createdAt: task.createdAt,
    });
  });

  test("AC-EDIT-5: a task id that doesn't exist shows \"Task not found\"", async ({ page }) => {
    await registerViaApi(page.request);

    await page.goto(`/tasks/${randomUUID()}`);

    await expect(page.getByText("Task not found")).toBeVisible();
    await expect(page.getByRole("link", { name: "Back to tasks" })).toBeVisible();
    await expect(page.getByLabel("Title", { exact: true })).toHaveCount(0);
  });

  test("AC-EDIT-6: a status change from the list updates the badge without a reload; other fields stay unchanged", async ({ page }) => {
    await registerViaApi(page.request);
    const task = await createTaskViaApi(page.request, {
      title: "Quick status",
      description: "unchanged description",
      priority: "high",
      dueDate: "2031-06-01T09:30:00Z",
    });
    await page.goto("/tasks");
    const row = taskRow(page, "Quick status");
    await expect(row.getByText("Status: To do")).toBeVisible();
    await markDocument(page);

    await row.getByLabel("Status for Quick status").selectOption("done");

    await expect(row.getByText("Status: Done")).toBeVisible();
    await expect(row.getByLabel("Status for Quick status")).toBeEnabled();
    await expectNoReloadSinceMark(page);
    const saved = await getTaskViaApi(page.request, task.id);
    expect(saved).toEqual({ ...task, status: "done", updatedAt: saved.updatedAt });
    expect(Date.parse(saved.updatedAt)).toBeGreaterThanOrEqual(Date.parse(task.updatedAt));
  });

  test("AC-DEL-2: delete asks for confirmation; cancel keeps the task, confirm deletes it and goes to /tasks", async ({ page }) => {
    await registerViaApi(page.request);
    const task = await createTaskViaApi(page.request, { title: "Delete me please" });
    await page.goto(`/tasks/${task.id}`);
    await expect(page.getByRole("heading", { name: "Edit task" })).toBeVisible();
    const dialog = page.getByRole("alertdialog", { name: "Delete this task?" });

    await page.getByRole("button", { name: "Delete task" }).click();
    await expect(dialog).toBeVisible();
    await expect(dialog).toContainText("Delete me please");
    await dialog.getByRole("button", { name: "Cancel" }).click();
    await expect(dialog).toHaveCount(0);
    await expect(page).toHaveURL(new RegExp(`/tasks/${task.id}$`));
    await getTaskViaApi(page.request, task.id); // still there

    await page.getByRole("button", { name: "Delete task" }).click();
    await expect(dialog).toBeVisible();
    await dialog.getByRole("button", { name: "Delete task" }).click();

    await expect(page).toHaveURL(/\/tasks$/);
    await expect(page.getByText("You have no tasks yet")).toBeVisible();
    expect((await page.request.get(`/api/v1/tasks/${task.id}`)).status()).toBe(404);
  });
});
