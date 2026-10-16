using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Dtos.Auth;
using TaskManager.Api.Dtos.Tasks;
using TaskManager.Api.IntegrationTests.Infrastructure;
using static TaskManager.Api.IntegrationTests.Infrastructure.ApiTestHelpers;

namespace TaskManager.Api.IntegrationTests;

/// <summary>
/// SPEC 12 (Tester agent): a two-user isolation test for each task and account endpoint.
/// User A and user B each have their own cookie jar (one <see cref="HttpClient"/> per browser session).
/// After every attempt by B, A's data is checked as A (through the API) and, where useful, in the database.
/// </summary>
public sealed class TwoUserIsolationTests : IClassFixture<TaskManagerApiFactory>
{
    private const string NewPassword = "batterystaple2";

    private readonly TaskManagerApiFactory _factory;

    public TwoUserIsolationTests(TaskManagerApiFactory factory)
    {
        _factory = factory;
    }

    // ---------- Tasks ----------

    [Fact]
    public async Task AC_LIST_1_Isolation_GetTasks_ReturnsOnlyCallersTasks_EvenWithMatchingFilters()
    {
        using var a = _factory.CreateApiClient();
        await RegisterAsync(a);
        using var b = _factory.CreateApiClient();
        await RegisterAsync(b);
        using var c = _factory.CreateApiClient();
        await RegisterAsync(c);

        var a1 = await CreateTaskAsync(a, new { title = "shared word alpha", status = "done", priority = "high" });
        var a2 = await CreateTaskAsync(a, new { title = "A only", description = "shared word in description" });
        var b1 = await CreateTaskAsync(b, new { title = "shared word beta", status = "done", priority = "high" });

        // B: plain list and lists whose filters would match A's tasks.
        foreach (var query in new[] { "", "?search=shared", "?status=done&priority=high", "?sortBy=title&sortDir=asc" })
        {
            var list = (await b.GetFromJsonAsync<List<TaskDto>>("/api/v1/tasks" + query, ApiJson))!;
            Assert.Equal([b1.Id], list.Select(t => t.Id));
        }

        // A sees exactly its own two tasks.
        var aList = (await a.GetFromJsonAsync<List<TaskDto>>("/api/v1/tasks", ApiJson))!;
        Assert.Equal(new[] { a1.Id, a2.Id }.Order(), aList.Select(t => t.Id).Order());

        // C has no tasks at all: an empty array, not B's or A's.
        Assert.Equal("[]", await c.GetStringAsync("/api/v1/tasks?search=shared"));
    }

    [Fact]
    public async Task AC_EDIT_2_Isolation_GetTaskById_OtherUsersTask_Is404_OwnerStillGets200()
    {
        using var a = _factory.CreateApiClient();
        await RegisterAsync(a);
        using var b = _factory.CreateApiClient();
        await RegisterAsync(b);
        var task = await CreateTaskAsync(a, new { title = "A private", description = "secret" });

        await AssertApiErrorAsync(await b.GetAsync($"/api/v1/tasks/{task.Id}"), HttpStatusCode.NotFound, "not_found");
        // Path ids are matched case-insensitively; an upper-case id must not leak the task either.
        await AssertApiErrorAsync(await b.GetAsync($"/api/v1/tasks/{task.Id.ToString().ToUpperInvariant()}"), HttpStatusCode.NotFound, "not_found");

        // AC-EDIT-1: the owner gets 200 with the same TaskDto.
        var ownerView = await a.GetFromJsonAsync<TaskDto>($"/api/v1/tasks/{task.Id}", ApiJson);
        Assert.Equal(task, ownerView);
    }

    [Fact]
    public async Task AC_EDIT_2_Isolation_PutOtherUsersTask_Is404_AndTaskIsUnchanged()
    {
        using var a = _factory.CreateApiClient();
        await RegisterAsync(a);
        using var b = _factory.CreateApiClient();
        await RegisterAsync(b);
        var due = _factory.Time.GetUtcNow().AddDays(2).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var task = await CreateTaskAsync(a, new { title = "A original", description = "keep", status = "todo", priority = "low", dueDate = due });
        _factory.Time.Advance(TimeSpan.FromSeconds(1)); // a real update would now change updatedAt

        var valid = new { title = "hacked by B", description = (string?)null, status = "done", priority = "high", dueDate = (string?)null };
        await AssertApiErrorAsync(await b.PutAsJsonAsync($"/api/v1/tasks/{task.Id}", valid, ApiJson), HttpStatusCode.NotFound, "not_found");

        // A changing past due date would be a 400 for the owner; for B the ownership lookup answers 404 first.
        var pastDue = new { title = "hacked by B", description = "x", status = "done", priority = "high", dueDate = "2020-01-01T00:00:00Z" };
        await AssertApiErrorAsync(await b.PutAsJsonAsync($"/api/v1/tasks/{task.Id}", pastDue, ApiJson), HttpStatusCode.NotFound, "not_found");

        var ownerView = await a.GetFromJsonAsync<TaskDto>($"/api/v1/tasks/{task.Id}", ApiJson);
        Assert.Equal(task, ownerView); // every field, including updatedAt
        Assert.Empty((await b.GetFromJsonAsync<List<TaskDto>>("/api/v1/tasks", ApiJson))!);
    }

    [Fact]
    public async Task AC_EDIT_2_Isolation_DeleteOtherUsersTask_Is404_AndTaskStillExists()
    {
        using var a = _factory.CreateApiClient();
        await RegisterAsync(a);
        using var b = _factory.CreateApiClient();
        await RegisterAsync(b);
        var task = await CreateTaskAsync(a, new { title = "A keeps this" });

        await AssertApiErrorAsync(await b.DeleteAsync($"/api/v1/tasks/{task.Id}"), HttpStatusCode.NotFound, "not_found");
        // A second attempt still sees 404 (the first one didn't delete anything either).
        await AssertApiErrorAsync(await b.DeleteAsync($"/api/v1/tasks/{task.Id}"), HttpStatusCode.NotFound, "not_found");

        Assert.Equal(task, await a.GetFromJsonAsync<TaskDto>($"/api/v1/tasks/{task.Id}", ApiJson));
        Assert.True(await _factory.WithDbAsync(db => db.Tasks.AnyAsync(t => t.Id == task.Id)));
    }

    [Fact]
    public async Task AC_CREATE_4_Isolation_PostTask_WithOtherUsersId_BelongsToCaller()
    {
        using var a = _factory.CreateApiClient();
        var userA = await RegisterAsync(a);
        using var b = _factory.CreateApiClient();
        var userB = await RegisterAsync(b);

        var created = await CreateTaskAsync(b, new { title = "B's task", userId = userA.Id });

        Assert.Equal(userB.Id, await _factory.WithDbAsync(db => db.Tasks.Where(t => t.Id == created.Id).Select(t => t.UserId).SingleAsync()));
        Assert.Equal("[]", await a.GetStringAsync("/api/v1/tasks"));
        await AssertApiErrorAsync(await a.GetAsync($"/api/v1/tasks/{created.Id}"), HttpStatusCode.NotFound, "not_found");
        Assert.Equal([created.Id], (await b.GetFromJsonAsync<List<TaskDto>>("/api/v1/tasks", ApiJson))!.Select(t => t.Id));
    }

    // ---------- Auth / account ----------

    [Fact]
    public async Task AC_AUTH_1_Isolation_GetMe_ReturnsCallersOwnUser()
    {
        using var a = _factory.CreateApiClient();
        var userA = await RegisterAsync(a);
        using var b = _factory.CreateApiClient();
        var userB = await RegisterAsync(b);
        Assert.NotEqual(userA.Id, userB.Id);

        Assert.Equal(userA, await a.GetFromJsonAsync<UserDto>("/api/v1/auth/me", ApiJson));
        Assert.Equal(userB, await b.GetFromJsonAsync<UserDto>("/api/v1/auth/me", ApiJson));

        // A fresh login as A in a third cookie jar is A, not whoever logged in last.
        using var a2 = _factory.CreateApiClient();
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(a2, userA.Email)).StatusCode);
        Assert.Equal(userA, await a2.GetFromJsonAsync<UserDto>("/api/v1/auth/me", ApiJson));
        Assert.Equal(userB, await b.GetFromJsonAsync<UserDto>("/api/v1/auth/me", ApiJson));
    }

    [Fact]
    public async Task AC_AUTH_8_Isolation_Logout_EndsOnlyCallersSession()
    {
        using var a = _factory.CreateApiClient();
        var userA = await RegisterAsync(a);
        using var b = _factory.CreateApiClient();
        await RegisterAsync(b);

        Assert.Equal(HttpStatusCode.NoContent, (await b.PostAsync("/api/v1/auth/logout", null)).StatusCode);
        _factory.Time.Advance(TimeSpan.FromSeconds(1));

        await AssertApiErrorAsync(await b.GetAsync("/api/v1/auth/me"), HttpStatusCode.Unauthorized, "unauthorized");
        Assert.Equal(userA, await a.GetFromJsonAsync<UserDto>("/api/v1/auth/me", ApiJson));
    }

    [Fact]
    public async Task AC_PROFILE_1_Isolation_UpdateProfile_ChangesOnlyCaller()
    {
        using var a = _factory.CreateApiClient();
        var userA = await RegisterAsync(a);
        using var b = _factory.CreateApiClient();
        var userB = await RegisterAsync(b);

        var response = await b.PutAsJsonAsync("/api/v1/account/profile", new { displayName = "  B Renamed  " }, ApiJson);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<UserDto>(ApiJson))!;
        Assert.Equal(userB with { DisplayName = "B Renamed" }, updated);
        Assert.Equal(updated, await b.GetFromJsonAsync<UserDto>("/api/v1/auth/me", ApiJson));

        // A is untouched, both through the API and in the database.
        Assert.Equal(userA, await a.GetFromJsonAsync<UserDto>("/api/v1/auth/me", ApiJson));
        Assert.Equal("Test User", await _factory.WithDbAsync(db => db.Users.Where(u => u.Id == userA.Id).Select(u => u.DisplayName).SingleAsync()));
    }

    [Fact]
    public async Task AC_PWD_1_Isolation_ChangePassword_AffectsOnlyCaller()
    {
        // Both users start with the same password, so a change applied to the wrong account would show up.
        using var a = _factory.CreateApiClient();
        var userA = await RegisterAsync(a);
        using var aSecondSession = _factory.CreateApiClient();
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(aSecondSession, userA.Email)).StatusCode);
        var aTask = await CreateTaskAsync(a, new { title = "A task survives B's password change" });
        using var b = _factory.CreateApiClient();
        var userB = await RegisterAsync(b);

        var change = await b.PostAsJsonAsync("/api/v1/account/change-password", new { currentPassword = Password, newPassword = NewPassword }, ApiJson);
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);

        // Security stamps are only re-checked when (now - issued) > interval (strictly), so move time on.
        _factory.Time.Advance(TimeSpan.FromSeconds(1));

        // A: both sessions still valid, the original password still works, the task is still there.
        Assert.Equal(userA, await a.GetFromJsonAsync<UserDto>("/api/v1/auth/me", ApiJson));
        Assert.Equal(userA, await aSecondSession.GetFromJsonAsync<UserDto>("/api/v1/auth/me", ApiJson));
        using var aFresh = _factory.CreateApiClient();
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(aFresh, userA.Email, Password)).StatusCode);
        using var aWithBsNewPassword = _factory.CreateApiClient();
        await AssertApiErrorAsync(await LoginAsync(aWithBsNewPassword, userA.Email, NewPassword), HttpStatusCode.Unauthorized, "invalid_credentials");
        Assert.Equal(aTask, await a.GetFromJsonAsync<TaskDto>($"/api/v1/tasks/{aTask.Id}", ApiJson));

        // B: same session still valid, old password fails, new one works.
        Assert.Equal(userB, await b.GetFromJsonAsync<UserDto>("/api/v1/auth/me", ApiJson));
        using var bOld = _factory.CreateApiClient();
        await AssertApiErrorAsync(await LoginAsync(bOld, userB.Email, Password), HttpStatusCode.Unauthorized, "invalid_credentials");
        using var bNew = _factory.CreateApiClient();
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(bNew, userB.Email, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task AC_DELACCT_1_Isolation_DeleteAccount_RemovesOnlyCallersUserAndTasks()
    {
        using var a = _factory.CreateApiClient();
        var userA = await RegisterAsync(a);
        var aTasks = new[]
        {
            await CreateTaskAsync(a, new { title = "A one" }),
            await CreateTaskAsync(a, new { title = "A two", description = "d", priority = "high" }),
        };
        using var b = _factory.CreateApiClient();
        var userB = await RegisterAsync(b);
        using var bSecondSession = _factory.CreateApiClient();
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(bSecondSession, userB.Email)).StatusCode);
        await CreateTaskAsync(b, new { title = "B one" });
        await CreateTaskAsync(b, new { title = "B two" });

        var delete = await b.PostAsJsonAsync("/api/v1/account/delete", new { password = Password }, ApiJson);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        _factory.Time.Advance(TimeSpan.FromSeconds(1));

        // B is gone: both sessions are 401, login fails, the cascade removed B's tasks.
        await AssertApiErrorAsync(await b.GetAsync("/api/v1/auth/me"), HttpStatusCode.Unauthorized, "unauthorized");
        await AssertApiErrorAsync(await bSecondSession.GetAsync("/api/v1/auth/me"), HttpStatusCode.Unauthorized, "unauthorized");
        using var bLogin = _factory.CreateApiClient();
        await AssertApiErrorAsync(await LoginAsync(bLogin, userB.Email), HttpStatusCode.Unauthorized, "invalid_credentials");
        Assert.Equal(0, await _factory.WithDbAsync(db => db.Tasks.CountAsync(t => t.UserId == userB.Id)));
        Assert.False(await _factory.WithDbAsync(db => db.Users.AnyAsync(u => u.Id == userB.Id)));

        // A is untouched: session, display name, login and tasks.
        Assert.Equal(userA, await a.GetFromJsonAsync<UserDto>("/api/v1/auth/me", ApiJson));
        using var aLogin = _factory.CreateApiClient();
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(aLogin, userA.Email)).StatusCode);
        var aList = (await a.GetFromJsonAsync<List<TaskDto>>("/api/v1/tasks", ApiJson))!;
        Assert.Equal(aTasks.OrderBy(t => t.Id), aList.OrderBy(t => t.Id));
        Assert.Equal(2, await _factory.WithDbAsync(db => db.Tasks.CountAsync(t => t.UserId == userA.Id)));
    }
}
