using System.Net;
using System.Net.Http.Json;
using TaskManager.Api.Dtos.Tasks;
using TaskManager.Api.IntegrationTests.Infrastructure;
using static TaskManager.Api.IntegrationTests.Infrastructure.ApiTestHelpers;

namespace TaskManager.Api.IntegrationTests;

/// <summary>Task endpoint wiring: binding, validation order, ownership, contract shape.</summary>
public sealed class TaskEndpointTests : IClassFixture<TaskManagerApiFactory>
{
    private readonly TaskManagerApiFactory _factory;

    public TaskEndpointTests(TaskManagerApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AC_CREATE_1_OnlyTitle_Returns201_RelativeLocation_AndDefaults()
    {
        using var client = _factory.CreateApiClient();
        await RegisterAsync(client);

        var response = await client.PostAsJsonAsync("/api/v1/tasks", new { title = "Buy milk", userId = Guid.NewGuid() }, ApiJson);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var task = (await response.Content.ReadFromJsonAsync<TaskDto>(ApiJson))!;
        Assert.Equal($"/api/v1/tasks/{task.Id}", response.Headers.Location?.OriginalString);
        Assert.False(response.Headers.Location!.IsAbsoluteUri);

        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"todo\"", json);
        Assert.Contains("\"priority\":\"medium\"", json);
        Assert.Contains("\"description\":null", json);
        Assert.Contains("\"dueDate\":null", json);
        Assert.Equal(task.CreatedAt, task.UpdatedAt);
        Assert.EndsWith("Z\"}", json);
    }

    [Fact]
    public async Task AC_DUE_3_And_4_DueDate_NormalizedToUtcWholeSeconds_OffsetRequired()
    {
        using var client = _factory.CreateApiClient();
        await RegisterAsync(client);

        var created = await client.PostAsJsonAsync("/api/v1/tasks", new { title = "t", dueDate = "2026-10-12T17:00:00.789+02:00" }, ApiJson);
        Assert.Contains("\"dueDate\":\"2026-10-12T15:00:00Z\"", await created.Content.ReadAsStringAsync());

        foreach (var bad in new[] { "2026-10-12T15:00:00", "2026-10-12" })
        {
            var response = await client.PostAsJsonAsync("/api/v1/tasks", new { title = "t", dueDate = bad }, ApiJson);
            var error = await AssertApiErrorAsync(response, HttpStatusCode.BadRequest, "validation_failed");
            Assert.Equal(["dueDate"], ErrorKeys(error));
        }
    }

    [Fact]
    public async Task AC_CREATE_2_InvalidOrNumericEnums_AreKeyedByField()
    {
        using var client = _factory.CreateApiClient();
        await RegisterAsync(client);

        var numeric = await client.PostAsJsonAsync("/api/v1/tasks", new { title = "t", priority = 2 }, ApiJson);
        Assert.Equal(["priority"], ErrorKeys(await AssertApiErrorAsync(numeric, HttpStatusCode.BadRequest, "validation_failed")));

        var unknown = await client.PostAsJsonAsync("/api/v1/tasks", new { title = " ", status = "finished" }, ApiJson);
        Assert.Equal(["status"], ErrorKeys(await AssertApiErrorAsync(unknown, HttpStatusCode.BadRequest, "validation_failed")));

        var blank = await client.PostAsJsonAsync("/api/v1/tasks", new { title = "  " }, ApiJson);
        Assert.Equal(["title"], ErrorKeys(await AssertApiErrorAsync(blank, HttpStatusCode.BadRequest, "validation_failed")));
    }

    [Fact]
    public async Task AC_EDIT_4_PutMissingKeys_Is400_KeyedByTheMissingFields()
    {
        using var client = _factory.CreateApiClient();
        await RegisterAsync(client);
        var task = await CreateTaskAsync(client, new { title = "t" });

        var missingNullable = await client.PutAsJsonAsync($"/api/v1/tasks/{task.Id}", new { title = "t", status = "done", priority = "low" }, ApiJson);
        Assert.Equal(["description", "dueDate"], ErrorKeys(await AssertApiErrorAsync(missingNullable, HttpStatusCode.BadRequest, "validation_failed")));

        var missingEnums = await client.PutAsJsonAsync($"/api/v1/tasks/{task.Id}", new { title = "t", description = (string?)null, dueDate = (string?)null }, ApiJson);
        Assert.Equal(["priority", "status"], ErrorKeys(await AssertApiErrorAsync(missingEnums, HttpStatusCode.BadRequest, "validation_failed")));
    }

    [Fact]
    public async Task AC_EDIT_2_AnotherUsersTask_Is404_AndUnchanged()
    {
        using var owner = _factory.CreateApiClient();
        await RegisterAsync(owner);
        var task = await CreateTaskAsync(owner, new { title = "private" });
        using var intruder = _factory.CreateApiClient();
        await RegisterAsync(intruder);
        var body = new { title = "hacked", description = (string?)null, status = "done", priority = "high", dueDate = (string?)null };

        await AssertApiErrorAsync(await intruder.GetAsync($"/api/v1/tasks/{task.Id}"), HttpStatusCode.NotFound, "not_found");
        await AssertApiErrorAsync(await intruder.PutAsJsonAsync($"/api/v1/tasks/{task.Id}", body, ApiJson), HttpStatusCode.NotFound, "not_found");
        await AssertApiErrorAsync(await intruder.DeleteAsync($"/api/v1/tasks/{task.Id}"), HttpStatusCode.NotFound, "not_found");
        Assert.Empty((await intruder.GetFromJsonAsync<List<TaskDto>>("/api/v1/tasks", ApiJson))!);

        var stillThere = await owner.GetFromJsonAsync<TaskDto>($"/api/v1/tasks/{task.Id}", ApiJson);
        Assert.Equal(task, stillThere);
    }

    [Fact]
    public async Task AC_LIST_7_InvalidQueryValues_Are400_KeyedByParameter()
    {
        using var client = _factory.CreateApiClient();
        await RegisterAsync(client);

        foreach (var (query, key) in new[]
                 {
                     ("status=1", "status"),
                     ("priority=urgent", "priority"),
                     ("sortBy=owner", "sortBy"),
                     ("sortDir=up", "sortDir"),
                     ("search=" + new string('s', 101), "search"),
                 })
        {
            var error = await AssertApiErrorAsync(await client.GetAsync("/api/v1/tasks?" + query), HttpStatusCode.BadRequest, "validation_failed");
            Assert.Equal([key], ErrorKeys(error));
        }

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/tasks?status=inProgress&priority=high&sortBy=dueDate&sortDir=asc&search=x")).StatusCode);
    }

    [Fact]
    public async Task AC_DUE_5_OverdueTask_ResendingSameInstant_Returns200()
    {
        using var client = _factory.CreateApiClient();
        await RegisterAsync(client);
        var due = _factory.Time.GetUtcNow().AddHours(1);
        var task = await CreateTaskAsync(client, new { title = "t", dueDate = due.ToString("yyyy-MM-ddTHH:mm:ssZ") });
        _factory.Time.Advance(TimeSpan.FromHours(3));

        var sameInstantOtherOffset = due.ToOffset(TimeSpan.FromHours(2)).ToString("yyyy-MM-ddTHH:mm:sszzz");
        var response = await client.PutAsJsonAsync(
            $"/api/v1/tasks/{task.Id}",
            new { title = "t", description = (string?)null, status = "done", priority = "medium", dueDate = sameInstantOtherOffset },
            ApiJson);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
