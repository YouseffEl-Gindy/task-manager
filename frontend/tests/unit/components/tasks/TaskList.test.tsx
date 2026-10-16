import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { TaskList } from "@/components/tasks/TaskList";
import { TaskStatusSelect } from "@/components/tasks/TaskStatusSelect";
import { SERVER_UNAVAILABLE_MESSAGE } from "@/lib/api/client";
import { makeTask } from "../../helpers/fixtures";
import { apiPath, installFetch, jsonResponse, textResponse } from "../../helpers/fetch";

describe("TaskList", () => {
  it("offers 'Create your first task' when the user has no tasks (AC-LIST-10)", () => {
    render(<TaskList tasks={[]} filtersActive={false} onClearFilters={vi.fn()} onTaskUpdated={vi.fn()} />);
    expect(screen.getByText("You have no tasks yet")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Create your first task" })).toHaveAttribute("href", "/tasks/new");
  });

  it("shows a no-results state with Clear filters when filters are active (AC-LIST-10)", async () => {
    const onClear = vi.fn();
    render(<TaskList tasks={[]} filtersActive onClearFilters={onClear} onTaskUpdated={vi.fn()} />);
    expect(screen.getByText("No tasks match your filters")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Clear filters" }));
    expect(onClear).toHaveBeenCalled();
  });

  it("renders each task with a title link, badges and status select", () => {
    const task = makeTask({ title: "Write report", status: "inProgress", priority: "high", description: "Q3" });
    render(<TaskList tasks={[task]} filtersActive={false} onClearFilters={vi.fn()} onTaskUpdated={vi.fn()} />);
    const item = within(screen.getByRole("list", { name: "Tasks" })).getByRole("listitem");
    expect(within(item).getByRole("link", { name: "Write report" })).toHaveAttribute("href", `/tasks/${task.id}`);
    expect(within(item).getByText("In progress", { selector: "span" })).toBeInTheDocument();
    expect(within(item).getByText("High")).toBeInTheDocument();
    expect(within(item).getByText("Q3")).toBeInTheDocument();
    expect(within(item).getByLabelText("Status for Write report")).toHaveValue("inProgress");
  });
});

describe("TaskStatusSelect", () => {
  it("PUTs every other field unchanged, including a past due date (AC-DUE-9, AC-EDIT-6)", async () => {
    const task = makeTask({
      title: "Pay rent",
      description: "Before noon",
      priority: "high",
      status: "todo",
      dueDate: "2026-10-01T08:00:00Z",
    });
    const updated = { ...task, status: "done" as const, updatedAt: "2026-10-11T12:00:00Z" };
    const { requests } = installFetch(() => jsonResponse(200, updated));
    const onUpdated = vi.fn();
    render(<TaskStatusSelect task={task} onUpdated={onUpdated} />);

    await userEvent.selectOptions(screen.getByLabelText("Status for Pay rent"), "done");

    expect(requests).toHaveLength(1);
    expect(requests[0].method).toBe("PUT");
    expect(apiPath(requests[0])).toBe(`/tasks/${task.id}`);
    expect(requests[0].headers.get("X-CSRF-Protection")).toBe("1");
    expect(requests[0].body).toEqual({
      title: "Pay rent",
      description: "Before noon",
      status: "done",
      priority: "high",
      dueDate: "2026-10-01T08:00:00Z",
    });
    expect(onUpdated).toHaveBeenCalledWith(updated);
  });

  it("shows an error and keeps the old value when the update fails", async () => {
    const task = makeTask({ title: "Pay rent" });
    installFetch(() => textResponse(502, "Bad gateway"));
    const onUpdated = vi.fn();
    render(<TaskStatusSelect task={task} onUpdated={onUpdated} />);
    await userEvent.selectOptions(screen.getByLabelText("Status for Pay rent"), "done");
    expect(await screen.findByRole("alert")).toHaveTextContent(SERVER_UNAVAILABLE_MESSAGE);
    expect(screen.getByLabelText("Status for Pay rent")).toHaveValue("todo");
    expect(onUpdated).not.toHaveBeenCalled();
  });
});
