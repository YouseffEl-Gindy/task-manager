import { fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { DueDateLabel } from "@/components/tasks/DueDateLabel";
import { DueDateTimeInput } from "@/components/tasks/DueDateTimeInput";

const NOW = new Date("2026-10-11T12:00:00Z");

describe("DueDateTimeInput (AC-DUE-8)", () => {
  it("shows a UTC value in local time", () => {
    render(<DueDateTimeInput value="2026-10-12T15:00:00Z" onChange={vi.fn()} />);
    const input = screen.getByLabelText(/Due date and time/);
    expect(input).toHaveAttribute("type", "datetime-local");
    expect(input).toHaveValue("2026-10-12T17:00");
    expect(screen.getByText(/Europe\/Berlin/)).toBeInTheDocument();
  });

  it("emits a UTC Z string for a local value and null when emptied", () => {
    const onChange = vi.fn();
    render(<DueDateTimeInput value={null} onChange={onChange} />);
    const input = screen.getByLabelText(/Due date and time/);
    fireEvent.change(input, { target: { value: "2026-12-24T18:30" } });
    expect(onChange).toHaveBeenLastCalledWith("2026-12-24T17:30:00Z");
  });

  it("has a clear button that sets null", async () => {
    const onChange = vi.fn();
    render(<DueDateTimeInput value="2026-10-12T15:00:00Z" onChange={onChange} />);
    await userEvent.click(screen.getByRole("button", { name: "Clear due date" }));
    expect(onChange).toHaveBeenCalledWith(null);
  });

  it("sets min to now in create mode only", () => {
    const { rerender } = render(<DueDateTimeInput value={null} onChange={vi.fn()} minNow />);
    expect(screen.getByLabelText(/Due date and time/)).toHaveAttribute("min");
    rerender(<DueDateTimeInput value={null} onChange={vi.fn()} />);
    expect(screen.getByLabelText(/Due date and time/)).not.toHaveAttribute("min");
  });
});

describe("DueDateLabel (AC-LIST-9)", () => {
  it("shows Overdue for a past due date when not done", () => {
    render(<DueDateLabel task={{ dueDate: "2026-10-10T08:00:00Z", status: "todo" }} now={NOW} />);
    expect(screen.getByText("Overdue")).toBeInTheDocument();
    expect(screen.getByText(/Oct/)).toHaveAttribute("dateTime", "2026-10-10T08:00:00Z");
  });

  it("does not show Overdue when the task is done", () => {
    render(<DueDateLabel task={{ dueDate: "2026-10-10T08:00:00Z", status: "done" }} now={NOW} />);
    expect(screen.queryByText("Overdue")).not.toBeInTheDocument();
  });

  it("does not show Overdue for a future date, and handles no due date", () => {
    const { rerender } = render(<DueDateLabel task={{ dueDate: "2026-10-12T08:00:00Z", status: "todo" }} now={NOW} />);
    expect(screen.queryByText("Overdue")).not.toBeInTheDocument();
    rerender(<DueDateLabel task={{ dueDate: null, status: "todo" }} now={NOW} />);
    expect(screen.getByText("No due date")).toBeInTheDocument();
  });
});
