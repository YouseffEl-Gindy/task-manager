import { expect, test } from "@playwright/test";
import { createTaskViaApi, registerViaApi, taskRow, taskTitles } from "./support/api";

test.describe("Task list: filter, search, sort, empty states", () => {
  test("AC-LIST-8: filters, search and sort update the URL and the list, and survive a reload", async ({ page }) => {
    await registerViaApi(page.request);
    // Created in this order, so the default sort (createdAt desc) is Charlie, Bravo, Alpha.
    await createTaskViaApi(page.request, { title: "Alpha report", priority: "high", status: "todo" });
    await createTaskViaApi(page.request, { title: "Bravo groceries", priority: "low", status: "done" });
    await createTaskViaApi(page.request, { title: "Charlie report", priority: "medium", status: "inProgress" });

    await page.goto("/tasks");
    const titles = taskTitles(page);
    const status = page.getByLabel("Status", { exact: true });
    const priority = page.getByLabel("Priority", { exact: true });
    const search = page.getByRole("searchbox", { name: "Search" });
    const sortBy = page.getByLabel("Sort by");
    await expect(titles).toHaveText(["Charlie report", "Bravo groceries", "Alpha report"]);

    // Status filter.
    await status.selectOption("done");
    await expect(page).toHaveURL(/\/tasks\?status=done$/);
    await expect(titles).toHaveText(["Bravo groceries"]);
    await page.reload();
    await expect(status).toHaveValue("done");
    await expect(titles).toHaveText(["Bravo groceries"]);
    await status.selectOption("all");
    await expect(page).toHaveURL(/\/tasks$/);

    // Priority filter.
    await priority.selectOption("high");
    await expect(page).toHaveURL(/\/tasks\?priority=high$/);
    await expect(titles).toHaveText(["Alpha report"]);
    await priority.selectOption("all");
    await expect(page).toHaveURL(/\/tasks$/);

    // Search (debounced), case-insensitive.
    await search.fill("REPORT");
    await expect(page).toHaveURL(/\/tasks\?search=REPORT$/);
    await expect(titles).toHaveText(["Charlie report", "Alpha report"]);

    // Sort by title, then flip the direction.
    await sortBy.selectOption("title");
    await expect(page).toHaveURL(/[?&]sortBy=title(&|$)/);
    await expect(titles).toHaveText(["Charlie report", "Alpha report"]); // desc is the default direction
    await page.getByRole("button", { name: /^Sort direction: Descending/ }).click();
    await expect(page).toHaveURL(/[?&]sortDir=asc(&|$)/);
    await expect(titles).toHaveText(["Alpha report", "Charlie report"]);

    // Reload keeps the whole view.
    await page.reload();
    await expect(search).toHaveValue("REPORT");
    await expect(sortBy).toHaveValue("title");
    await expect(page.getByRole("button", { name: /^Sort direction: Ascending/ })).toBeVisible();
    await expect(titles).toHaveText(["Alpha report", "Charlie report"]);
    expect(new URL(page.url()).searchParams.get("search")).toBe("REPORT");
  });

  test("AC-LIST-8: priority sort orders low < medium < high", async ({ page }) => {
    await registerViaApi(page.request);
    await createTaskViaApi(page.request, { title: "P medium", priority: "medium" });
    await createTaskViaApi(page.request, { title: "P high", priority: "high" });
    await createTaskViaApi(page.request, { title: "P low", priority: "low" });

    await page.goto("/tasks?sortBy=priority&sortDir=asc");

    await expect(page.getByLabel("Sort by")).toHaveValue("priority");
    await expect(taskTitles(page)).toHaveText(["P low", "P medium", "P high"]);
  });

  test("AC-LIST-10: a user with no tasks sees \"Create your first task\"; no results shows an empty state", async ({ page }) => {
    await registerViaApi(page.request);

    await page.goto("/tasks");
    await expect(page.getByText("You have no tasks yet")).toBeVisible();
    const createFirst = page.getByRole("link", { name: "Create your first task" });
    await createFirst.click();
    await expect(page).toHaveURL(/\/tasks\/new$/);
    await expect(page.getByRole("heading", { name: "New task" })).toBeVisible();

    await createTaskViaApi(page.request, { title: "The only task" });
    await page.goto("/tasks?search=zzz-no-match");
    await expect(page.getByText("No tasks match your filters")).toBeVisible();
    await expect(createFirst).toHaveCount(0);
    await page.getByRole("button", { name: "Clear filters" }).click();
    await expect(page).toHaveURL(/\/tasks$/);
    await expect(taskRow(page, "The only task")).toBeVisible();
  });
});
