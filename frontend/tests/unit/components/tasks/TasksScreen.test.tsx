import { act, fireEvent, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { AuthGuard } from "@/components/auth/AuthGuard";
import { SEARCH_DEBOUNCE_MS } from "@/components/tasks/TaskSearchInput";
import { TasksScreen } from "@/components/tasks/TasksScreen";
import { SERVER_UNAVAILABLE_MESSAGE } from "@/lib/api/client";
import { navigation, setSearchParams } from "../../mocks/nextNavigation";
import { USER, makeTask } from "../../helpers/fixtures";
import { apiErrorResponse, apiPath, installFetch, jsonResponse, textResponse } from "../../helpers/fetch";
import { renderWithAuth } from "../../helpers/render";

afterEach(() => {
  vi.useRealTimers();
});

describe("TasksScreen", () => {
  it("loads the list for the URL query and shows the tasks (AC-LIST-8)", async () => {
    setSearchParams("status=todo&priority=high&search=report&sortBy=dueDate&sortDir=asc&bogus=1");
    const task = makeTask({ title: "Write quarterly report", priority: "high" });
    const { requests } = installFetch(() => jsonResponse(200, [task]));
    renderWithAuth(<TasksScreen />, { user: USER });

    expect(screen.getByText("Loading tasks…")).toBeInTheDocument();
    expect(await screen.findByRole("link", { name: "Write quarterly report" })).toBeInTheDocument();
    expect(apiPath(requests[0])).toBe("/tasks");
    expect(requests[0].url.search).toBe("?status=todo&priority=high&search=report&sortBy=dueDate&sortDir=asc");

    // Controls reflect the URL.
    expect(screen.getByLabelText("Status")).toHaveValue("todo");
    expect(screen.getByLabelText("Priority")).toHaveValue("high");
    expect(screen.getByLabelText("Search")).toHaveValue("report");
    expect(screen.getByLabelText("Sort by")).toHaveValue("dueDate");
  });

  it("never sends invalid URL values to the API", async () => {
    setSearchParams("status=7&sortBy=nonsense");
    const { requests } = installFetch(() => jsonResponse(200, []));
    renderWithAuth(<TasksScreen />, { user: USER });
    await screen.findByText("You have no tasks yet");
    expect(requests[0].url.search).toBe("");
  });

  it("writes filter and sort changes to the URL (AC-LIST-8)", async () => {
    setSearchParams("search=milk");
    installFetch(() => jsonResponse(200, []));
    renderWithAuth(<TasksScreen />, { user: USER });
    await screen.findByText("No tasks match your filters");

    await userEvent.selectOptions(screen.getByLabelText("Status"), "done");
    expect(navigation.router.replace).toHaveBeenLastCalledWith("/tasks?status=done&search=milk", { scroll: false });

    await userEvent.selectOptions(screen.getByLabelText("Priority"), "low");
    expect(navigation.router.replace).toHaveBeenLastCalledWith("/tasks?priority=low&search=milk", { scroll: false });

    await userEvent.selectOptions(screen.getByLabelText("Sort by"), "priority");
    expect(navigation.router.replace).toHaveBeenLastCalledWith("/tasks?search=milk&sortBy=priority", { scroll: false });

    await userEvent.click(screen.getByRole("button", { name: /Sort direction: Descending/ }));
    expect(navigation.router.replace).toHaveBeenLastCalledWith("/tasks?search=milk&sortDir=asc", { scroll: false });

    await userEvent.click(screen.getByRole("button", { name: "Clear filters" }));
    expect(navigation.router.replace).toHaveBeenLastCalledWith("/tasks", { scroll: false });
  });

  it("debounces search by 300 ms before writing it to the URL", async () => {
    installFetch(() => jsonResponse(200, []));
    renderWithAuth(<TasksScreen />, { user: USER });
    await screen.findByText("You have no tasks yet");

    vi.useFakeTimers();
    fireEvent.change(screen.getByLabelText("Search"), { target: { value: "  milk " } });
    act(() => {
      vi.advanceTimersByTime(SEARCH_DEBOUNCE_MS - 1);
    });
    expect(navigation.router.replace).not.toHaveBeenCalled();
    act(() => {
      vi.advanceTimersByTime(1);
    });
    expect(navigation.router.replace).toHaveBeenCalledWith("/tasks?search=milk", { scroll: false });
  });

  it("offers 'Create your first task' when there are no tasks at all (AC-LIST-10)", async () => {
    installFetch(() => jsonResponse(200, []));
    renderWithAuth(<TasksScreen />, { user: USER });
    expect(await screen.findByRole("link", { name: "Create your first task" })).toHaveAttribute("href", "/tasks/new");
    expect(screen.getByRole("link", { name: "New task" })).toHaveAttribute("href", "/tasks/new");
  });

  it("updates the badge after a status change without a reload (AC-EDIT-6)", async () => {
    const task = makeTask({ title: "Pay rent", status: "todo" });
    installFetch((r) => (r.method === "PUT" ? jsonResponse(200, { ...task, status: "done" }) : jsonResponse(200, [task])));
    renderWithAuth(<TasksScreen />, { user: USER });
    const item = (await screen.findByRole("link", { name: "Pay rent" })).closest("li");
    expect(item).not.toBeNull();
    expect(within(item as HTMLElement).getByText("To do", { selector: "span" })).toBeInTheDocument();

    await userEvent.selectOptions(screen.getByLabelText("Status for Pay rent"), "done");
    expect(await within(item as HTMLElement).findByText("Done", { selector: "span" })).toBeInTheDocument();
  });

  it("shows the server-unavailable message for a non-JSON 502 and does not redirect (AC-ERR-5)", async () => {
    let calls = 0;
    installFetch(() => {
      calls += 1;
      return calls === 1 ? textResponse(502, "<html><h1>502 Bad Gateway</h1></html>") : jsonResponse(200, []);
    });
    renderWithAuth(<TasksScreen />, { user: USER });
    expect(await screen.findByText(SERVER_UNAVAILABLE_MESSAGE)).toBeInTheDocument();
    expect(navigation.router.replace).not.toHaveBeenCalled();

    await userEvent.click(screen.getByRole("button", { name: "Try again" }));
    expect(await screen.findByText("You have no tasks yet")).toBeInTheDocument();
  });

  it("shows the server-unavailable message on a network failure (AC-ERR-5)", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("Failed to fetch")));
    renderWithAuth(<TasksScreen />, { user: USER });
    expect(await screen.findByText(SERVER_UNAVAILABLE_MESSAGE)).toBeInTheDocument();
  });

  it("redirects to login when the list call returns 401 unauthorized (AC-ERR-4)", async () => {
    window.history.replaceState(null, "", "/tasks?status=done");
    setSearchParams("status=done");
    installFetch((r) => (apiPath(r) === "/auth/me" ? jsonResponse(200, USER) : apiErrorResponse(401, "unauthorized")));
    renderWithAuth(
      <AuthGuard>
        <TasksScreen />
      </AuthGuard>,
      { user: USER },
    );
    await vi.waitFor(() =>
      expect(navigation.router.replace).toHaveBeenCalledWith("/login?returnUrl=%2Ftasks%3Fstatus%3Ddone"),
    );
  });
});
