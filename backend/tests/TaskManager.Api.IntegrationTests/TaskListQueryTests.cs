using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using TaskManager.Api.Dtos.Tasks;
using TaskManager.Api.IntegrationTests.Infrastructure;
using static TaskManager.Api.IntegrationTests.Infrastructure.ApiTestHelpers;

namespace TaskManager.Api.IntegrationTests;

/// <summary>
/// HTTP-level tests for list filtering, search and sorting (AC-LIST-2 to AC-LIST-6).
/// Each test uses its own user, so every assertion is about that user's tasks only (never global counts).
/// Case-insensitivity data is ASCII only, which both SQLite (NOCASE/LIKE) and SQL Server (CI collation) fold.
/// Tasks are created one fake second apart, so createdAt order is well defined; the provider-dependent id
/// tie-break is never asserted.
/// </summary>
public sealed class TaskListQueryTests : IClassFixture<TaskManagerApiFactory>
{
    private readonly TaskManagerApiFactory _factory;

    public TaskListQueryTests(TaskManagerApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AC_LIST_2_NoParameters_SortsByCreatedAtDescending()
    {
        using var client = await NewUserAsync();
        var first = await CreateSpacedAsync(client, new { title = "first" });
        var second = await CreateSpacedAsync(client, new { title = "second", priority = "high" });
        var third = await CreateSpacedAsync(client, new { title = "third", priority = "low" });

        Assert.Equal([third.Id, second.Id, first.Id], await IdsAsync(client, ""));
        // Explicit defaults give the same order.
        Assert.Equal([third.Id, second.Id, first.Id], await IdsAsync(client, "?sortBy=createdAt&sortDir=desc"));
        Assert.Equal([first.Id, second.Id, third.Id], await IdsAsync(client, "?sortBy=createdAt&sortDir=asc"));
    }

    [Fact]
    public async Task AC_LIST_3_StatusAndPriorityFilters_EachWork_AndCombineWithAnd()
    {
        using var client = await NewUserAsync();
        var todoLow = await CreateSpacedAsync(client, new { title = "todo low", status = "todo", priority = "low" });
        var todoHigh = await CreateSpacedAsync(client, new { title = "todo high", status = "todo", priority = "high" });
        var doneHigh = await CreateSpacedAsync(client, new { title = "done high", status = "done", priority = "high" });
        var progressMedium = await CreateSpacedAsync(client, new { title = "in progress medium", status = "inProgress", priority = "medium" });

        Assert.Equal([todoHigh.Id, todoLow.Id], await IdsAsync(client, "?status=todo"));
        Assert.Equal([progressMedium.Id], await IdsAsync(client, "?status=inProgress"));
        Assert.Equal([doneHigh.Id, todoHigh.Id], await IdsAsync(client, "?priority=high"));
        Assert.Equal([todoLow.Id], await IdsAsync(client, "?priority=low"));
        Assert.Equal([todoHigh.Id], await IdsAsync(client, "?status=todo&priority=high"));
        Assert.Equal([doneHigh.Id], await IdsAsync(client, "?priority=high&status=done"));
        Assert.Empty(await IdsAsync(client, "?status=done&priority=low"));
        // Filters also combine with search.
        Assert.Equal([todoHigh.Id], await IdsAsync(client, "?status=todo&search=HIGH"));
    }

    [Fact]
    public async Task AC_LIST_4_SearchIsCaseInsensitive_OnTitleOrDescription_AndSpecialCharactersMatchLiterally()
    {
        using var client = await NewUserAsync();
        var milkTitle = await CreateSpacedAsync(client, new { title = "Buy milk" });
        var milkDescription = await CreateSpacedAsync(client, new { title = "Groceries", description = "oat MILK please" });
        await CreateSpacedAsync(client, new { title = "Bread", description = "no dairy" });
        var percent = await CreateSpacedAsync(client, new { title = "100% done" });
        await CreateSpacedAsync(client, new { title = "100 done" });
        var underscore = await CreateSpacedAsync(client, new { title = "a_b" });
        await CreateSpacedAsync(client, new { title = "axb" });
        var backslash = await CreateSpacedAsync(client, new { title = "back\\slash" });
        await CreateSpacedAsync(client, new { title = "back/slash" });
        var bracket = await CreateSpacedAsync(client, new { title = "[draft] notes" });
        await CreateSpacedAsync(client, new { title = "draft notes" });

        foreach (var search in new[] { "MiLk", "milk", "MILK", "  milk  " })
        {
            Assert.Equal([milkDescription.Id, milkTitle.Id], await IdsAsync(client, Search(search)));
        }

        Assert.Equal([percent.Id], await IdsAsync(client, Search("%")));
        Assert.Equal([percent.Id], await IdsAsync(client, Search("0% D")));
        Assert.Equal([underscore.Id], await IdsAsync(client, Search("_")));
        Assert.Equal([backslash.Id], await IdsAsync(client, Search("\\")));
        Assert.Equal([bracket.Id], await IdsAsync(client, Search("[")));
        Assert.Equal([bracket.Id], await IdsAsync(client, Search("[DRAFT]")));
        Assert.Empty(await IdsAsync(client, Search("[a-z]")));
        Assert.Equal(11, (await IdsAsync(client, Search("   "))).Count); // blank search = no filter
    }

    [Fact]
    public async Task AC_LIST_5_PrioritySortsLowFirst_DueDateSortsByInstantWithNullsLastBothWays()
    {
        using var client = await NewUserAsync();
        var medium = await CreateSpacedAsync(client, new { title = "medium", priority = "medium" });
        var high = await CreateSpacedAsync(client, new { title = "high", priority = "high" });
        var low = await CreateSpacedAsync(client, new { title = "low", priority = "low" });

        Assert.Equal([low.Id, medium.Id, high.Id], await IdsAsync(client, "?sortBy=priority&sortDir=asc"));
        Assert.Equal([high.Id, medium.Id, low.Id], await IdsAsync(client, "?sortBy=priority&sortDir=desc"));
        Assert.Equal([high.Id, medium.Id, low.Id], await IdsAsync(client, "?sortBy=priority")); // desc is the default

        using var dueClient = await NewUserAsync();
        var now = _factory.Time.GetUtcNow();
        // As local wall-clock text, "later" sorts before "earlier" here; only the instant gives the right order.
        var earlier = now.AddDays(1);                  // written at +14:00
        var later = now.AddDays(1).AddHours(1);        // written at -12:00
        var latest = now.AddDays(3);                   // written as Z
        var noDueOld = await CreateSpacedAsync(dueClient, new { title = "no due (older)" });
        var dueLatest = await CreateSpacedAsync(dueClient, new { title = "latest", dueDate = Iso(latest) });
        var dueEarlier = await CreateSpacedAsync(dueClient, new { title = "earlier", dueDate = Iso(earlier.ToOffset(TimeSpan.FromHours(14))) });
        var noDueNew = await CreateSpacedAsync(dueClient, new { title = "no due (newer)" });
        var dueLater = await CreateSpacedAsync(dueClient, new { title = "later", dueDate = Iso(later.ToOffset(TimeSpan.FromHours(-12))) });

        // Null due dates come last in both directions (among themselves: tie-breaker createdAt desc).
        Assert.Equal(
            [dueEarlier.Id, dueLater.Id, dueLatest.Id, noDueNew.Id, noDueOld.Id],
            await IdsAsync(dueClient, "?sortBy=dueDate&sortDir=asc"));
        Assert.Equal(
            [dueLatest.Id, dueLater.Id, dueEarlier.Id, noDueNew.Id, noDueOld.Id],
            await IdsAsync(dueClient, "?sortBy=dueDate&sortDir=desc"));
    }

    [Fact]
    public async Task AC_LIST_6_TitleSort_IsCaseInsensitive_AppleBananaCherry()
    {
        using var client = await NewUserAsync();
        var cherry = await CreateSpacedAsync(client, new { title = "cherry" });
        var apple = await CreateSpacedAsync(client, new { title = "apple" });
        var banana = await CreateSpacedAsync(client, new { title = "Banana" });

        Assert.Equal([apple.Id, banana.Id, cherry.Id], await IdsAsync(client, "?sortBy=title&sortDir=asc"));
        Assert.Equal([cherry.Id, banana.Id, apple.Id], await IdsAsync(client, "?sortBy=title&sortDir=desc"));

        // An ordinal (case-sensitive) sort would put "Banana" first; check the titles too for a readable failure.
        var titles = (await client.GetFromJsonAsync<List<TaskDto>>("/api/v1/tasks?sortBy=title&sortDir=asc", ApiJson))!.Select(t => t.Title);
        Assert.Equal(["apple", "Banana", "cherry"], titles);
    }

    private async Task<HttpClient> NewUserAsync()
    {
        var client = _factory.CreateApiClient();
        await RegisterAsync(client);
        return client;
    }

    /// <summary>Creates a task, then moves the fake clock 1 s so the next task has a later createdAt.</summary>
    private async Task<TaskDto> CreateSpacedAsync(HttpClient client, object body)
    {
        var task = await CreateTaskAsync(client, body);
        _factory.Time.Advance(TimeSpan.FromSeconds(1));
        return task;
    }

    private static async Task<List<Guid>> IdsAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync("/api/v1/tasks" + query);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{query}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<List<TaskDto>>(ApiJson))!.Select(t => t.Id).ToList();
    }

    private static string Search(string text) => "?search=" + Uri.EscapeDataString(text);

    private static string Iso(DateTimeOffset value) =>
        value.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
}
