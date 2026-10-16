using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TaskManager.Api.Dtos.Tasks;
using TaskManager.Api.Entities;
using TaskManager.Api.IntegrationTests.Infrastructure;
using static TaskManager.Api.IntegrationTests.Infrastructure.ApiTestHelpers;

namespace TaskManager.Api.IntegrationTests;

/// <summary>
/// HTTP-level tests for the task acceptance criteria (SPEC 9) that were only unit-tested before:
/// AC-CREATE-2 (lengths), AC-CREATE-3, AC-DUE-1, 2, 6, 7, AC-EDIT-3, AC-DEL-1.
/// Every time value comes from the factory's FakeTimeProvider, which only ever moves forward.
/// </summary>
public sealed class TaskRuleTests : IClassFixture<TaskManagerApiFactory>
{
    private readonly TaskManagerApiFactory _factory;

    public TaskRuleTests(TaskManagerApiFactory factory)
    {
        _factory = factory;
    }

    private DateTimeOffset Now => _factory.Time.GetUtcNow();

    // ---------- AC-CREATE-2 / AC-CREATE-3 ----------

    [Fact]
    public async Task AC_CREATE_2_TitleOver200_DescriptionOver2000_MissingTitle_MalformedDueDate_Are400KeyedByField()
    {
        using var client = _factory.CreateApiClient();
        await RegisterAsync(client);

        var cases = new (object Body, string Key)[]
        {
            (new { title = new string('t', 201) }, "title"),
            (new { title = "  " + new string('t', 201) + "  " }, "title"),
            (new { title = "ok", description = new string('d', 2001) }, "description"),
            (new { description = "no title key" }, "title"),
            (new { title = (string?)null }, "title"),
            (new { title = "ok", dueDate = "next tuesday" }, "dueDate"),
            (new { title = "ok", dueDate = "2031-02-30T10:00:00Z" }, "dueDate"),
        };

        foreach (var (body, key) in cases)
        {
            var response = await client.PostAsJsonAsync("/api/v1/tasks", body, ApiJson);
            var error = await AssertApiErrorAsync(response, HttpStatusCode.BadRequest, "validation_failed");
            Assert.Equal([key], ErrorKeys(error));
        }

        Assert.Equal("[]", await client.GetStringAsync("/api/v1/tasks"));

        // Exactly at the limits is fine (lengths after trimming).
        var atLimit = await CreateTaskAsync(client, new { title = " " + new string('t', 200) + " ", description = " " + new string('d', 2000) + " " });
        Assert.Equal(200, atLimit.Title.Length);
        Assert.Equal(2000, atLimit.Description!.Length);
    }

    [Fact]
    public async Task AC_CREATE_3_TitleAndDescriptionAreTrimmed_WhitespaceOnlyDescriptionIsNull()
    {
        using var client = _factory.CreateApiClient();
        await RegisterAsync(client);

        var trimmed = await CreateTaskAsync(client, new { title = "  Buy milk \t", description = "\n 2 liters, oat  " });
        Assert.Equal("Buy milk", trimmed.Title);
        Assert.Equal("2 liters, oat", trimmed.Description);

        var blank = await CreateTaskAsync(client, new { title = "blank description", description = " \t\n " });
        Assert.Null(blank.Description);

        // Stored that way, not just echoed.
        Assert.Equal(trimmed, await client.GetFromJsonAsync<TaskDto>($"/api/v1/tasks/{trimmed.Id}", ApiJson));
        Assert.Contains("\"description\":null", await client.GetStringAsync($"/api/v1/tasks/{blank.Id}"));

        // The same rules apply on PUT.
        var put = await client.PutAsJsonAsync(
            $"/api/v1/tasks/{trimmed.Id}",
            new { title = "  Buy oat milk  ", description = "   ", status = "todo", priority = "medium", dueDate = (string?)null },
            ApiJson);
        var updated = (await put.Content.ReadFromJsonAsync<TaskDto>(ApiJson))!;
        Assert.Equal("Buy oat milk", updated.Title);
        Assert.Null(updated.Description);
    }

    // ---------- AC-DUE-1 / AC-DUE-2 ----------

    [Fact]
    public async Task AC_DUE_1_CreateWithDueDateOneHourAhead_Returns201()
    {
        using var client = _factory.CreateApiClient();
        await RegisterAsync(client);
        var due = Now.AddHours(1);

        var response = await client.PostAsJsonAsync("/api/v1/tasks", new { title = "due soon", dueDate = Iso(due) }, ApiJson);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var task = (await response.Content.ReadFromJsonAsync<TaskDto>(ApiJson))!;
        Assert.Equal(due.UtcDateTime, task.DueDate);
    }

    [Fact]
    public async Task AC_DUE_2_CreateWithDueDateInThePastOrEqualToNow_Is400KeyedDueDate()
    {
        using var client = _factory.CreateApiClient();
        await RegisterAsync(client);
        var now = Now;
        Assert.Equal(0, now.Millisecond); // the factory clock moves in whole seconds, so "equal to now" is exact

        foreach (var dueDate in new[]
                 {
                     Iso(now),                                    // equal to now
                     Iso(now.ToOffset(TimeSpan.FromHours(5))),    // equal to now, other offset
                     IsoWithFraction(now.AddMilliseconds(500)),   // truncated to now
                     Iso(now.AddSeconds(-1)),
                     Iso(now.AddYears(-1)),
                 })
        {
            var response = await client.PostAsJsonAsync("/api/v1/tasks", new { title = "past", dueDate }, ApiJson);
            var error = await AssertApiErrorAsync(response, HttpStatusCode.BadRequest, "validation_failed");
            Assert.Equal(["dueDate"], ErrorKeys(error));
        }

        Assert.Equal("[]", await client.GetStringAsync("/api/v1/tasks"));
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/tasks", new { title = "next second", dueDate = Iso(now.AddSeconds(1)) }, ApiJson)).StatusCode);
    }

    // ---------- AC-DUE-6 / AC-DUE-7 ----------

    [Fact]
    public async Task AC_DUE_6_PutChangingDueDateToADifferentPastInstant_OrFromNullToPast_Is400KeyedDueDate()
    {
        using var client = _factory.CreateApiClient();
        await RegisterAsync(client);
        var withDue = await CreateTaskAsync(client, new { title = "has due", dueDate = Iso(Now.AddHours(1)) });
        var withoutDue = await CreateTaskAsync(client, new { title = "no due" });
        _factory.Time.Advance(TimeSpan.FromHours(3)); // withDue is now overdue

        foreach (var (task, pastDue) in new[]
                 {
                     (withDue, Iso(Now.AddHours(-1))),                          // a different past instant
                     (withDue, Iso(withDue.DueDate!.Value.AddSeconds(-1))),      // 1 s earlier than the stored one
                     (withoutDue, Iso(Now.AddMinutes(-1))),                      // null -> past
                     (withoutDue, Iso(Now)),                                     // null -> exactly now
                 })
        {
            var response = await client.PutAsJsonAsync($"/api/v1/tasks/{task.Id}", FullBody(task, dueDate: pastDue, status: "done"), ApiJson);
            var error = await AssertApiErrorAsync(response, HttpStatusCode.BadRequest, "validation_failed");
            Assert.Equal(["dueDate"], ErrorKeys(error));
        }

        // Nothing changed.
        Assert.Equal(withDue, await client.GetFromJsonAsync<TaskDto>($"/api/v1/tasks/{withDue.Id}", ApiJson));
        Assert.Equal(withoutDue, await client.GetFromJsonAsync<TaskDto>($"/api/v1/tasks/{withoutDue.Id}", ApiJson));
    }

    [Fact]
    public async Task AC_DUE_7_PutOnAnOverdueTask_SettingDueDateToNullOrToTheFuture_Returns200()
    {
        using var client = _factory.CreateApiClient();
        await RegisterAsync(client);
        var first = await CreateTaskAsync(client, new { title = "clear me", dueDate = Iso(Now.AddHours(1)) });
        var second = await CreateTaskAsync(client, new { title = "move me", dueDate = Iso(Now.AddHours(1)) });
        _factory.Time.Advance(TimeSpan.FromHours(2)); // both overdue

        var cleared = await client.PutAsJsonAsync($"/api/v1/tasks/{first.Id}", FullBody(first, dueDate: null), ApiJson);
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        Assert.Null((await cleared.Content.ReadFromJsonAsync<TaskDto>(ApiJson))!.DueDate);

        var newDue = Now.AddDays(1);
        var moved = await client.PutAsJsonAsync($"/api/v1/tasks/{second.Id}", FullBody(second, dueDate: Iso(newDue.ToOffset(TimeSpan.FromHours(-7)))), ApiJson);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal(newDue.UtcDateTime, (await moved.Content.ReadFromJsonAsync<TaskDto>(ApiJson))!.DueDate);

        Assert.Null((await client.GetFromJsonAsync<TaskDto>($"/api/v1/tasks/{first.Id}", ApiJson))!.DueDate);
        Assert.Equal(newDue.UtcDateTime, (await client.GetFromJsonAsync<TaskDto>($"/api/v1/tasks/{second.Id}", ApiJson))!.DueDate);
    }

    // ---------- AC-EDIT-3 ----------

    [Fact]
    public async Task AC_EDIT_3_ValidPut_ReplacesFields_RefreshesUpdatedAt_KeepsCreatedAt_NullsClear()
    {
        using var client = _factory.CreateApiClient();
        await RegisterAsync(client);
        var original = await CreateTaskAsync(client, new { title = "Original", description = "first", status = "todo", priority = "low", dueDate = Iso(Now.AddDays(1)) });
        _factory.Time.Advance(TimeSpan.FromMinutes(5));
        var newDue = Now.AddDays(3);

        var replaced = await client.PutAsJsonAsync(
            $"/api/v1/tasks/{original.Id}",
            new { title = "Replaced", description = "second", status = "inProgress", priority = "high", dueDate = Iso(newDue) },
            ApiJson);

        Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);
        var afterReplace = (await replaced.Content.ReadFromJsonAsync<TaskDto>(ApiJson))!;
        Assert.Equal(
            original with
            {
                Title = "Replaced",
                Description = "second",
                Status = TaskItemStatus.InProgress,
                Priority = TaskItemPriority.High,
                DueDate = newDue.UtcDateTime,
                UpdatedAt = Now.UtcDateTime,
            },
            afterReplace);
        Assert.Equal(original.CreatedAt, afterReplace.CreatedAt);
        Assert.True(afterReplace.UpdatedAt > original.UpdatedAt);
        Assert.Equal(afterReplace, await client.GetFromJsonAsync<TaskDto>($"/api/v1/tasks/{original.Id}", ApiJson));

        // Sending null for description and dueDate clears them.
        _factory.Time.Advance(TimeSpan.FromMinutes(1));
        var cleared = await client.PutAsJsonAsync(
            $"/api/v1/tasks/{original.Id}",
            new { title = "Replaced", description = (string?)null, status = "done", priority = "medium", dueDate = (string?)null },
            ApiJson);
        var afterClear = (await cleared.Content.ReadFromJsonAsync<TaskDto>(ApiJson))!;
        Assert.Null(afterClear.Description);
        Assert.Null(afterClear.DueDate);
        Assert.Equal(TaskItemStatus.Done, afterClear.Status);
        Assert.Equal(original.CreatedAt, afterClear.CreatedAt);
        Assert.Equal(Now.UtcDateTime, afterClear.UpdatedAt);
    }

    // ---------- AC-DEL-1 ----------

    [Fact]
    public async Task AC_DEL_1_Delete_Returns204_ThenGetIs404_AndASecondDeleteIs404()
    {
        using var client = _factory.CreateApiClient();
        await RegisterAsync(client);
        var task = await CreateTaskAsync(client, new { title = "delete me" });
        var keep = await CreateTaskAsync(client, new { title = "keep me" });

        var response = await client.DeleteAsync($"/api/v1/tasks/{task.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
        await AssertApiErrorAsync(await client.GetAsync($"/api/v1/tasks/{task.Id}"), HttpStatusCode.NotFound, "not_found");
        await AssertApiErrorAsync(await client.DeleteAsync($"/api/v1/tasks/{task.Id}"), HttpStatusCode.NotFound, "not_found");
        var list = (await client.GetFromJsonAsync<List<TaskDto>>("/api/v1/tasks", ApiJson))!;
        Assert.Equal([keep.Id], list.Select(t => t.Id));
    }

    // ---------- helpers ----------

    private static string Iso(DateTimeOffset value) =>
        value.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);

    private static string Iso(DateTime utc) =>
        utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string IsoWithFraction(DateTimeOffset value) =>
        value.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture);

    /// <summary>A full PUT body that keeps the task's current values except the given ones.</summary>
    private static Dictionary<string, object?> FullBody(TaskDto task, string? dueDate, string? status = null) => new()
    {
        ["title"] = task.Title,
        ["description"] = task.Description,
        ["status"] = status ?? JsonNamingPolicy.CamelCase.ConvertName(task.Status.ToString()),
        ["priority"] = JsonNamingPolicy.CamelCase.ConvertName(task.Priority.ToString()),
        ["dueDate"] = dueDate,
    };
}
