using Microsoft.Extensions.Time.Testing;
using TaskManager.Api.Dtos.Tasks;
using TaskManager.Api.Entities;
using TaskManager.Api.Services;
using TaskManager.Api.UnitTests.TestSupport;

namespace TaskManager.Api.UnitTests.Services;

/// <summary>List filtering, search (with LIKE escaping) and sorting, run as real SQL on SQLite (SPEC FR-T6 to FR-T8).</summary>
public sealed class TaskListQueryTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteTestDatabase _database = new();
    private readonly FakeTimeProvider _time = new(Start);

    public void Dispose() => _database.Dispose();

    private async Task<TaskDto> AddAsync(
        Guid userId,
        string title,
        string? description = null,
        TaskItemStatus? status = null,
        TaskItemPriority? priority = null,
        DateTimeOffset? due = null)
    {
        _time.Advance(TimeSpan.FromSeconds(1)); // distinct createdAt values
        await using var db = _database.CreateContext();
        var result = await new TaskService(db, _time).CreateAsync(userId, new CreateTaskRequest(title, description, status, priority, due), CancellationToken.None);
        return result.Value!;
    }

    private async Task<IReadOnlyList<TaskDto>> ListAsync(Guid userId, TaskListQuery query)
    {
        await using var db = _database.CreateContext();
        return await new TaskService(db, _time).ListAsync(userId, query, CancellationToken.None);
    }

    private async Task<IReadOnlyList<string>> TitlesAsync(Guid userId, TaskListQuery query) =>
        (await ListAsync(userId, query)).Select(t => t.Title).ToList();

    [Fact]
    public async Task List_ReturnsOnlyTheCallersTasks_EmptyWhenNone()
    {
        var me = await _database.AddUserAsync();
        var other = await _database.AddUserAsync();
        var lonely = await _database.AddUserAsync();
        await AddAsync(me, "mine");
        await AddAsync(other, "theirs");

        Assert.Equal(["mine"], await TitlesAsync(me, new TaskListQuery()));
        Assert.Empty(await ListAsync(lonely, new TaskListQuery()));
    }

    [Fact]
    public async Task List_DefaultSort_IsCreatedAtDescending()
    {
        var me = await _database.AddUserAsync();
        await AddAsync(me, "first");
        await AddAsync(me, "second");
        await AddAsync(me, "third");

        Assert.Equal(["third", "second", "first"], await TitlesAsync(me, new TaskListQuery()));
        Assert.Equal(["first", "second", "third"], await TitlesAsync(me, new TaskListQuery { SortDir = SortDirection.Asc }));
    }

    [Fact]
    public async Task List_StatusAndPriorityFilters_CombineWithAnd()
    {
        var me = await _database.AddUserAsync();
        await AddAsync(me, "todo-high", status: TaskItemStatus.Todo, priority: TaskItemPriority.High);
        await AddAsync(me, "todo-low", status: TaskItemStatus.Todo, priority: TaskItemPriority.Low);
        await AddAsync(me, "done-high", status: TaskItemStatus.Done, priority: TaskItemPriority.High);

        Assert.Equal(["todo-low", "todo-high"], await TitlesAsync(me, new TaskListQuery { Status = TaskItemStatus.Todo }));
        Assert.Equal(["done-high", "todo-high"], await TitlesAsync(me, new TaskListQuery { Priority = TaskItemPriority.High }));
        Assert.Equal(["todo-high"], await TitlesAsync(me, new TaskListQuery { Status = TaskItemStatus.Todo, Priority = TaskItemPriority.High }));
    }

    [Fact]
    public async Task Search_IsCaseInsensitive_OnTitleOrDescription()
    {
        var me = await _database.AddUserAsync();
        await AddAsync(me, "Buy milk");
        await AddAsync(me, "Groceries", "eggs and MILK");
        await AddAsync(me, "Unrelated", "bread");

        Assert.Equal(["Groceries", "Buy milk"], await TitlesAsync(me, new TaskListQuery { Search = "  MiLk " }));
    }

    [Theory]
    [InlineData("%", "100% done")]
    [InlineData("_", "snake_case")]
    [InlineData("\\", "back\\slash")]
    [InlineData("[", "[draft] plan")]
    public async Task Search_SpecialCharacters_MatchLiterally(string search, string expectedTitle)
    {
        var me = await _database.AddUserAsync();
        foreach (var title in new[] { "100% done", "100 percent", "snake_case", "snakeXcase", "back\\slash", "backslash", "[draft] plan", "draft plan" })
        {
            await AddAsync(me, title);
        }

        Assert.Equal([expectedTitle], await TitlesAsync(me, new TaskListQuery { Search = search }));
    }

    [Fact]
    public async Task Search_Blank_MeansNoFilter()
    {
        var me = await _database.AddUserAsync();
        await AddAsync(me, "a");
        await AddAsync(me, "b");

        Assert.Equal(2, (await ListAsync(me, new TaskListQuery { Search = "   " })).Count);
    }

    [Fact]
    public async Task SortByPriority_LowBeforeMediumBeforeHigh()
    {
        var me = await _database.AddUserAsync();
        await AddAsync(me, "high", priority: TaskItemPriority.High);
        await AddAsync(me, "low", priority: TaskItemPriority.Low);
        await AddAsync(me, "medium", priority: TaskItemPriority.Medium);

        Assert.Equal(["low", "medium", "high"], await TitlesAsync(me, new TaskListQuery { SortBy = TaskSortBy.Priority, SortDir = SortDirection.Asc }));
        Assert.Equal(["high", "medium", "low"], await TitlesAsync(me, new TaskListQuery { SortBy = TaskSortBy.Priority, SortDir = SortDirection.Desc }));
    }

    [Fact]
    public async Task SortByDueDate_NullsLastInBothDirections_OrderedByInstant()
    {
        var me = await _database.AddUserAsync();
        await AddAsync(me, "none-1");
        // 10:00+02:00 is 08:00Z, earlier than 09:00Z even though its local clock reads later.
        await AddAsync(me, "later", due: new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero));
        await AddAsync(me, "earlier", due: new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.FromHours(2)));
        await AddAsync(me, "none-2");

        Assert.Equal(["earlier", "later", "none-2", "none-1"], await TitlesAsync(me, new TaskListQuery { SortBy = TaskSortBy.DueDate, SortDir = SortDirection.Asc }));
        Assert.Equal(["later", "earlier", "none-2", "none-1"], await TitlesAsync(me, new TaskListQuery { SortBy = TaskSortBy.DueDate, SortDir = SortDirection.Desc }));
    }

    [Fact]
    public async Task SortByTitle_IsCaseInsensitive()
    {
        var me = await _database.AddUserAsync();
        await AddAsync(me, "cherry");
        await AddAsync(me, "Banana");
        await AddAsync(me, "apple");

        Assert.Equal(["apple", "Banana", "cherry"], await TitlesAsync(me, new TaskListQuery { SortBy = TaskSortBy.Title, SortDir = SortDirection.Asc }));
        Assert.Equal(["cherry", "Banana", "apple"], await TitlesAsync(me, new TaskListQuery { SortBy = TaskSortBy.Title, SortDir = SortDirection.Desc }));
    }

    [Fact]
    public async Task TieBreaker_IsCreatedAtDescending()
    {
        var me = await _database.AddUserAsync();
        await AddAsync(me, "old", priority: TaskItemPriority.High);
        await AddAsync(me, "new", priority: TaskItemPriority.High);

        Assert.Equal(["new", "old"], await TitlesAsync(me, new TaskListQuery { SortBy = TaskSortBy.Priority, SortDir = SortDirection.Asc }));
    }
}
