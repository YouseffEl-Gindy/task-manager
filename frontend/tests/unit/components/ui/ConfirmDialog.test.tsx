import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { ConfirmDialog } from "@/components/ui/ConfirmDialog";

describe("ConfirmDialog", () => {
  it("renders nothing when closed", () => {
    render(<ConfirmDialog open={false} title="Sure?" onConfirm={vi.fn()} onCancel={vi.fn()} />);
    expect(screen.queryByRole("alertdialog")).not.toBeInTheDocument();
  });

  it("is a labelled modal that focuses Cancel and handles Escape", async () => {
    const onCancel = vi.fn();
    const onConfirm = vi.fn();
    render(
      <ConfirmDialog open title="Delete this task?" onConfirm={onConfirm} onCancel={onCancel}>
        It will be gone.
      </ConfirmDialog>,
    );
    const dialog = screen.getByRole("alertdialog", { name: "Delete this task?" });
    expect(dialog).toHaveAttribute("aria-modal", "true");
    expect(dialog).toHaveAccessibleDescription("It will be gone.");
    expect(screen.getByRole("button", { name: "Cancel" })).toHaveFocus();

    await userEvent.keyboard("{Escape}");
    expect(onCancel).toHaveBeenCalledTimes(1);

    await userEvent.click(screen.getByRole("button", { name: "Confirm" }));
    expect(onConfirm).toHaveBeenCalledTimes(1);
  });

  it("keeps Tab focus inside the dialog", async () => {
    render(<ConfirmDialog open title="Sure?" onConfirm={vi.fn()} onCancel={vi.fn()} />);
    const cancel = screen.getByRole("button", { name: "Cancel" });
    const confirm = screen.getByRole("button", { name: "Confirm" });
    await userEvent.tab();
    expect(confirm).toHaveFocus();
    await userEvent.tab();
    expect(cancel).toHaveFocus();
  });
});
