import { describe, expect, it } from "vitest";
import { changePassword, deleteAccount, updateProfile } from "@/lib/api/account";
import { getCurrentUser, login, logout, register } from "@/lib/api/auth";
import { createTask, deleteTask, getTask, listTasks, updateTask } from "@/lib/api/tasks";
import { USER, makeTask } from "../../helpers/fixtures";
import { apiPath, installFetch, jsonResponse, noContent } from "../../helpers/fetch";

describe("auth endpoints", () => {
  it("register / login POST with CSRF and return the UserDto", async () => {
    const { requests } = installFetch(() => jsonResponse(201, USER));
    await expect(register({ displayName: "Ada", email: "ada@example.com", password: "correcthorse1" })).resolves.toEqual(USER);
    await expect(login({ email: "ada@example.com", password: "correcthorse1" })).resolves.toEqual(USER);
    expect(requests.map((r) => [r.method, apiPath(r)])).toEqual([
      ["POST", "/auth/register"],
      ["POST", "/auth/login"],
    ]);
    expect(requests.every((r) => r.headers.get("X-CSRF-Protection") === "1")).toBe(true);
  });

  it("logout POSTs without a body; me GETs", async () => {
    const { requests } = installFetch((r) => (r.method === "GET" ? jsonResponse(200, USER) : noContent()));
    await logout();
    await getCurrentUser();
    expect(requests[0]).toMatchObject({ method: "POST", body: undefined });
    expect(apiPath(requests[0])).toBe("/auth/logout");
    expect(apiPath(requests[1])).toBe("/auth/me");
  });
});

describe("account endpoints", () => {
  it("use the documented methods and paths", async () => {
    const { requests } = installFetch((r) => (r.method === "PUT" ? jsonResponse(200, USER) : noContent()));
    await updateProfile({ displayName: "Ada L." });
    await changePassword({ currentPassword: "a1234567", newPassword: "b1234567" });
    await deleteAccount({ password: "b1234567" });
    expect(requests.map((r) => [r.method, apiPath(r), r.body])).toEqual([
      ["PUT", "/account/profile", { displayName: "Ada L." }],
      ["POST", "/account/change-password", { currentPassword: "a1234567", newPassword: "b1234567" }],
      ["POST", "/account/delete", { password: "b1234567" }],
    ]);
  });
});

describe("task endpoints", () => {
  it("listTasks sends only the set query keys", async () => {
    const { requests } = installFetch(() => jsonResponse(200, []));
    await listTasks({ status: "inProgress", search: "  milk ", sortBy: "dueDate", sortDir: "asc" });
    expect(apiPath(requests[0])).toBe("/tasks");
    expect(requests[0].url.search).toBe("?status=inProgress&search=milk&sortBy=dueDate&sortDir=asc");
    await listTasks();
    expect(requests[1].url.search).toBe("");
  });

  it("get / update / delete use the encoded id", async () => {
    const task = makeTask();
    const { requests } = installFetch((r) => (r.method === "DELETE" ? noContent() : jsonResponse(200, task)));
    await getTask(task.id);
    await updateTask(task.id, {
      title: task.title,
      description: null,
      status: "done",
      priority: "low",
      dueDate: null,
    });
    await deleteTask("a/b");
    expect(requests.map((r) => [r.method, apiPath(r)])).toEqual([
      ["GET", `/tasks/${task.id}`],
      ["PUT", `/tasks/${task.id}`],
      ["DELETE", "/tasks/a%2Fb"],
    ]);
    expect(requests[1].body).toEqual({ title: task.title, description: null, status: "done", priority: "low", dueDate: null });
  });

  it("createTask POSTs the body", async () => {
    const task = makeTask({ title: "Buy milk" });
    const { requests } = installFetch(() => jsonResponse(201, task));
    await expect(createTask({ title: "Buy milk" })).resolves.toEqual(task);
    expect(requests[0]).toMatchObject({ method: "POST", body: { title: "Buy milk" } });
  });
});
