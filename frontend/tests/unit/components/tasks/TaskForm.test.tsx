import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import type { UpdateTaskRequest } from "@/types/api";
import { DUE_DATE_PAST_MESSAGE, TaskForm } from "@/components/tasks/TaskForm";
import { ApiResponseError } from "@/lib/api/client";
import { apiError } from "../../helpers/fetch";
import { makeTask } from "../../helpers/fixtures";

const NOW = new Date("2026-10-11T12:00:00Z"); // 14:00 in Europe/Berlin
const now = () => NOW;

function dueInput(): HTMLInputElement {
  return screen.getByLabelText(/Due date and time/) as HTMLInputElement;
}

describe("TaskForm: create", () => {
  it("shows an inline error for an empty title and does not submit (AC-CREATE-5)", async () => {
    const onSubmit = vi.fn<(values: UpdateTaskRequest) => Promise<void>>();
    render(<TaskForm mode="create" onSubmit={onSubmit} now={now} />);
    await userEvent.type(screen.getByLabelText("Title"), "   ");
    await userEvent.click(screen.getByRole("button", { name: "Create task" }));
    expect(screen.getByText("Title is required.")).toBeInTheDocument();
    expect(screen.getByLabelText("Title")).toHaveAttribute("aria-invalid", "true");
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it("submits trimmed values with the defaults", async () => {
    const onSubmit = vi.fn<(values: UpdateTaskRequest) => Promise<void>>().mockResolvedValue();
    render(<TaskForm mode="create" onSubmit={onSubmit} now={now} />);
    await userEvent.type(screen.getByLabelText("Title"), "  Buy milk  ");
    await userEvent.type(screen.getByLabelText("Description (optional)"), "   ");
    await userEvent.click(screen.getByRole("button", { name: "Create task" }));
    expect(onSubmit).toHaveBeenCalledWith({
      title: "Buy milk",
      description: null,
      status: "todo",
      priority: "medium",
      dueDate: null,
    });
  });

  it("sends the local due date as a UTC Z string (AC-DUE-8)", async () => {
    const onSubmit = vi.fn<(values: UpdateTaskRequest) => Promise<void>>().mockResolvedValue();
    render(<TaskForm mode="create" onSubmit={onSubmit} now={now} />);
    await userEvent.type(screen.getByLabelText("Title"), "Report");
    await userEvent.selectOptions(screen.getByLabelText("Priority"), "high");
    fireEvent.change(dueInput(), { target: { value: "2026-10-12T17:00" } });
    await userEvent.click(screen.getByRole("button", { name: "Create task" }));
    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ title: "Report", priority: "high", dueDate: "2026-10-12T15:00:00Z" }),
    );
  });

  it("rejects a past due date inline (AC-DUE-8)", async () => {
    const onSubmit = vi.fn<(values: UpdateTaskRequest) => Promise<void>>();
    render(<TaskForm mode="create" onSubmit={onSubmit} now={now} />);
    await userEvent.type(screen.getByLabelText("Title"), "Report");
    fireEvent.change(dueInput(), { target: { value: "2026-10-11T13:59" } });
    await userEvent.click(screen.getByRole("button", { name: "Create task" }));
    expect(screen.getByText(DUE_DATE_PAST_MESSAGE)).toBeInTheDocument();
    expect(dueInput()).toHaveAttribute("aria-invalid", "true");
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it("shows server field errors next to the inputs", async () => {
    const onSubmit = vi
      .fn<(values: UpdateTaskRequest) => Promise<void>>()
      .mockRejectedValue(
        new ApiResponseError(apiError(400, "validation_failed", "One or more fields are invalid.", { dueDate: ["Due date must be in the future."] })),
      );
    render(<TaskForm mode="create" onSubmit={onSubmit} now={now} />);
    await userEvent.type(screen.getByLabelText("Title"), "Report");
    fireEvent.change(dueInput(), { target: { value: "2026-10-11T14:01" } });
    await userEvent.click(screen.getByRole("button", { name: "Create task" }));
    expect(await screen.findByText("Due date must be in the future.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Create task" })).toBeEnabled();
  });
});

describe("TaskForm: edit", () => {
  const overdue = makeTask({
    title: "Pay rent",
    description: "Before noon",
    status: "inProgress",
    priority: "high",
    dueDate: "2026-10-01T08:00:00Z",
  });

  it("loads the current values, with the due date in local time (AC-EDIT-5)", () => {
    render(<TaskForm mode="edit" initialTask={overdue} onSubmit={vi.fn()} now={now} />);
    expect(screen.getByLabelText("Title")).toHaveValue("Pay rent");
    expect(screen.getByLabelText("Description (optional)")).toHaveValue("Before noon");
    expect(screen.getByLabelText("Status")).toHaveValue("inProgress");
    expect(screen.getByLabelText("Priority")).toHaveValue("high");
    expect(dueInput()).toHaveValue("2026-10-01T10:00");
  });

  it("allows saving with an unchanged past due date and re-sends it exactly", async () => {
    const onSubmit = vi.fn<(values: UpdateTaskRequest) => Promise<void>>().mockResolvedValue();
    render(<TaskForm mode="edit" initialTask={overdue} onSubmit={onSubmit} now={now} />);
    await userEvent.selectOptions(screen.getByLabelText("Status"), "done");
    await userEvent.click(screen.getByRole("button", { name: "Save changes" }));
    expect(onSubmit).toHaveBeenCalledWith({
      title: "Pay rent",
      description: "Before noon",
      status: "done",
      priority: "high",
      dueDate: "2026-10-01T08:00:00Z",
    });
  });

  it("rejects changing the due date to a different past instant", async () => {
    const onSubmit = vi.fn<(values: UpdateTaskRequest) => Promise<void>>();
    render(<TaskForm mode="edit" initialTask={overdue} onSubmit={onSubmit} now={now} />);
    fireEvent.change(dueInput(), { target: { value: "2026-10-02T10:00" } });
    await userEvent.click(screen.getByRole("button", { name: "Save changes" }));
    expect(screen.getByText(DUE_DATE_PAST_MESSAGE)).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it("allows clearing the due date (null)", async () => {
    const onSubmit = vi.fn<(values: UpdateTaskRequest) => Promise<void>>().mockResolvedValue();
    render(<TaskForm mode="edit" initialTask={overdue} onSubmit={onSubmit} now={now} />);
    await userEvent.click(screen.getByRole("button", { name: "Clear due date" }));
    expect(dueInput()).toHaveValue("");
    await userEvent.click(screen.getByRole("button", { name: "Save changes" }));
    await waitFor(() => expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ dueDate: null })));
  });
});
