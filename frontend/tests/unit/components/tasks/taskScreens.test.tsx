import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { DeleteTaskButton } from "@/components/tasks/DeleteTaskButton";
import { EditTaskScreen } from "@/components/tasks/EditTaskScreen";
import { NewTaskScreen } from "@/components/tasks/NewTaskScreen";
import { navigation } from "../../mocks/nextNavigation";
import { USER, makeTask } from "../../helpers/fixtures";
import { apiErrorResponse, apiPath, installFetch, jsonResponse, noContent } from "../../helpers/fetch";
import { renderWithAuth } from "../../helpers/render";

describe("NewTaskScreen (AC-CREATE-5)", () => {
  it("creates the task and goes to /tasks", async () => {
    const created = makeTask({ title: "Buy milk" });
    const { requests } = installFetch(() => jsonResponse(201, created));
    renderWithAuth(<NewTaskScreen />, { user: USER });
    await userEvent.type(screen.getByLabelText("Title"), "Buy milk");
    await userEvent.click(screen.getByRole("button", { name: "Create task" }));
    await waitFor(() => expect(navigation.router.push).toHaveBeenCalledWith("/tasks"));
    expect(requests[0]).toMatchObject({ method: "POST", body: { title: "Buy milk", status: "todo", priority: "medium" } });
    expect(apiPath(requests[0])).toBe("/tasks");
  });

  it("makes no API call for an empty title", async () => {
    const { mock } = installFetch(() => jsonResponse(201, makeTask()));
    renderWithAuth(<NewTaskScreen />, { user: USER });
    await userEvent.click(screen.getByRole("button", { name: "Create task" }));
    expect(screen.getByText("Title is required.")).toBeInTheDocument();
    expect(mock).not.toHaveBeenCalled();
  });
});

describe("EditTaskScreen (AC-EDIT-5)", () => {
  it("loads the task, saves with PUT, and goes to /tasks", async () => {
    const task = makeTask({ title: "Pay rent", dueDate: "2026-10-01T08:00:00Z" });
    const { requests } = installFetch((r) => jsonResponse(200, r.method === "PUT" ? { ...task, title: "Pay rent now" } : task));
    renderWithAuth(<EditTaskScreen id={task.id} />, { user: USER });
    expect(screen.getByText("Loading task…")).toBeInTheDocument();
    const title = await screen.findByLabelText("Title");
    expect(title).toHaveValue("Pay rent");

    await userEvent.type(title, " now");
    await userEvent.click(screen.getByRole("button", { name: "Save changes" }));
    await waitFor(() => expect(navigation.router.push).toHaveBeenCalledWith("/tasks"));
    const put = requests.find((r) => r.method === "PUT");
    expect(put && apiPath(put)).toBe(`/tasks/${task.id}`);
    expect(put?.body).toEqual({
      title: "Pay rent now",
      description: null,
      status: "todo",
      priority: "medium",
      dueDate: "2026-10-01T08:00:00Z",
    });
  });

  it("shows 'Task not found' on a 404", async () => {
    installFetch(() => apiErrorResponse(404, "not_found", "The requested resource was not found."));
    renderWithAuth(<EditTaskScreen id="does-not-exist" />, { user: USER });
    expect(await screen.findByText("Task not found")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Back to tasks" })).toHaveAttribute("href", "/tasks");
  });
});

describe("DeleteTaskButton (AC-DEL-2)", () => {
  it("does nothing on cancel", async () => {
    const { mock } = installFetch(() => noContent());
    renderWithAuth(<DeleteTaskButton taskId="t1" taskTitle="Pay rent" />, { user: USER });
    await userEvent.click(screen.getByRole("button", { name: "Delete task" }));
    const dialog = screen.getByRole("alertdialog", { name: "Delete this task?" });
    expect(dialog).toHaveTextContent("Pay rent");
    await userEvent.click(within(dialog).getByRole("button", { name: "Cancel" }));
    expect(screen.queryByRole("alertdialog")).not.toBeInTheDocument();
    expect(mock).not.toHaveBeenCalled();
  });

  it("deletes on confirm and goes to /tasks", async () => {
    const { requests } = installFetch(() => noContent());
    renderWithAuth(<DeleteTaskButton taskId="t1" taskTitle="Pay rent" />, { user: USER });
    await userEvent.click(screen.getByRole("button", { name: "Delete task" }));
    await userEvent.click(within(screen.getByRole("alertdialog")).getByRole("button", { name: "Delete task" }));
    await waitFor(() => expect(navigation.router.push).toHaveBeenCalledWith("/tasks"));
    expect(requests[0].method).toBe("DELETE");
    expect(apiPath(requests[0])).toBe("/tasks/t1");
    expect(requests[0].headers.get("X-CSRF-Protection")).toBe("1");
  });
});
