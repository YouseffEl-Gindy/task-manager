using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using TaskManager.Api.Dtos.Tasks;
using TaskManager.Api.Entities;
using TaskManager.Api.Services;
using TaskManager.Api.UnitTests.TestSupport;
using TaskManager.Api.Validation;

namespace TaskManager.Api.UnitTests.Services;

/// <summary>TaskService on SQLite in-memory: defaults, trimming, ownership, the update due-date rule.</summary>
public sealed class TaskServiceTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteTestDatabase _database = new();
    private readonly FakeTimeProvider _time = new(Start);

    public void Dispose() => _database.Dispose();

    private async Task<T> WithServiceAsync<T>(Func<TaskService, Task<T>> action)
    {
        await using var db = _database.CreateContext();
        return await action(new TaskService(db, _time));
    }

    private Task<ServiceResult<TaskDto>> CreateAsync(Guid userId, CreateTaskRequest request) =>
        WithServiceAsync(s => s.CreateAsync(userId, request, CancellationToken.None));

    private Task<ServiceResult<TaskDto>> UpdateAsync(Guid userId, Guid taskId, UpdateTaskRequest request) =>
        WithServiceAsync(s => s.UpdateAsync(userId, taskId, request, CancellationToken.None));

    private static UpdateTaskRequest UpdateFrom(TaskDto task, DateTimeOffset? dueDate, TaskItemStatus? status = null) =>
        new(task.Title, task.Description, status ?? task.Status, task.Priority, dueDate);

    [Fact]
    public async Task Create_OnlyTitle_AppliesDefaults_AndSetsTimestampsFromTimeProvider()
    {
        var userId = await _database.AddUserAsync();

        var result = await CreateAsync(userId, new CreateTaskRequest("  Buy milk  ", null, null, null, null));

        Assert.Equal(ServiceOutcome.Success, result.Outcome);
        var task = result.Value!;
        Assert.NotEqual(Guid.Empty, task.Id);
        Assert.Equal("Buy milk", task.Title);
        Assert.Null(task.Description);
        Assert.Equal(TaskItemStatus.Todo, task.Status);
        Assert.Equal(TaskItemPriority.Medium, task.Priority);
        Assert.Null(task.DueDate);
        Assert.Equal(Start.UtcDateTime, task.CreatedAt);
        Assert.Equal(task.CreatedAt, task.UpdatedAt);
        Assert.Equal(DateTimeKind.Utc, task.CreatedAt.Kind);
    }

    [Fact]
    public async Task Create_TrimsDescription_AndStoresWhitespaceOnlyAsNull()
    {
        var userId = await _database.AddUserAsync();

        var blank = await CreateAsync(userId, new CreateTaskRequest("t", "   ", null, null, null));
        var trimmed = await CreateAsync(userId, new CreateTaskRequest("t", "  2 liters  ", null, null, null));

        Assert.Null(blank.Value!.Description);
        Assert.Equal("2 liters", trimmed.Value!.Description);
    }

    [Fact]
    public async Task Create_NormalizesDueDateToUtcWholeSeconds()
    {
        var userId = await _database.AddUserAsync();
        var due = new DateTimeOffset(2026, 10, 12, 17, 0, 0, 789, TimeSpan.FromHours(2));

        var created = (await CreateAsync(userId, new CreateTaskRequest("t", null, TaskItemStatus.InProgress, TaskItemPriority.High, due))).Value!;
        var reloaded = await WithServiceAsync(s => s.GetAsync(userId, created.Id, CancellationToken.None));

        var expected = new DateTime(2026, 10, 12, 15, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expected, created.DueDate);
        Assert.Equal(expected, reloaded!.DueDate);
        Assert.Equal(DateTimeKind.Utc, reloaded.DueDate!.Value.Kind);
        Assert.Equal(TaskItemStatus.InProgress, reloaded.Status);
        Assert.Equal(TaskItemPriority.High, reloaded.Priority);
    }

    [Fact]
    public async Task Create_ForAUserThatNoLongerExists_IsUnauthorized()
    {
        var result = await CreateAsync(Guid.NewGuid(), new CreateTaskRequest("t", null, null, null, null));

        Assert.Equal(ServiceOutcome.Unauthorized, result.Outcome);
    }

    [Fact]
    public async Task GetUpdateDelete_AnotherUsersTask_BehaveAsNotFound_AndLeaveItUnchanged()
    {
        var owner = await _database.AddUserAsync();
        var other = await _database.AddUserAsync();
        var task = (await CreateAsync(owner, new CreateTaskRequest("mine", null, null, null, null))).Value!;

        Assert.Null(await WithServiceAsync(s => s.GetAsync(other, task.Id, CancellationToken.None)));
        Assert.Equal(ServiceOutcome.NotFound, (await UpdateAsync(other, task.Id, UpdateFrom(task, null, TaskItemStatus.Done))).Outcome);
        Assert.False(await WithServiceAsync(s => s.DeleteAsync(other, task.Id, CancellationToken.None)));

        var unchanged = await WithServiceAsync(s => s.GetAsync(owner, task.Id, CancellationToken.None));
        Assert.Equal(task, unchanged);
    }

    [Fact]
    public async Task Update_ReplacesFields_RefreshesUpdatedAt_KeepsCreatedAt()
    {
        var userId = await _database.AddUserAsync();
        var task = (await CreateAsync(userId, new CreateTaskRequest("t", "desc", null, null, Start.AddDays(1)))).Value!;
        _time.Advance(TimeSpan.FromMinutes(5));

        var result = await UpdateAsync(userId, task.Id, new UpdateTaskRequest(" New title ", null, TaskItemStatus.Done, TaskItemPriority.Low, null));

        Assert.Equal(ServiceOutcome.Success, result.Outcome);
        var updated = result.Value!;
        Assert.Equal("New title", updated.Title);
        Assert.Null(updated.Description);
        Assert.Null(updated.DueDate);
        Assert.Equal(TaskItemStatus.Done, updated.Status);
        Assert.Equal(TaskItemPriority.Low, updated.Priority);
        Assert.Equal(task.CreatedAt, updated.CreatedAt);
        Assert.Equal(Start.AddMinutes(5).UtcDateTime, updated.UpdatedAt);
    }

    [Fact]
    public async Task Update_UnknownTask_IsNotFound_BeforeTheDueDateRule()
    {
        var userId = await _database.AddUserAsync();

        var result = await UpdateAsync(userId, Guid.NewGuid(), new UpdateTaskRequest("t", null, TaskItemStatus.Todo, TaskItemPriority.Low, Start.AddDays(-1)));

        Assert.Equal(ServiceOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task Update_OverdueTask_ResendingTheSameInstant_IsAllowed_EvenWithADifferentOffset()
    {
        var userId = await _database.AddUserAsync();
        var due = Start.AddHours(1);
        var task = (await CreateAsync(userId, new CreateTaskRequest("t", null, null, null, due))).Value!;
        _time.Advance(TimeSpan.FromDays(2));

        var sameUtc = await UpdateAsync(userId, task.Id, UpdateFrom(task, due, TaskItemStatus.Done));
        var sameOtherOffset = await UpdateAsync(userId, task.Id, UpdateFrom(task, due.ToOffset(TimeSpan.FromHours(-7)), TaskItemStatus.InProgress));

        Assert.Equal(ServiceOutcome.Success, sameUtc.Outcome);
        Assert.Equal(TaskItemStatus.Done, sameUtc.Value!.Status);
        Assert.Equal(ServiceOutcome.Success, sameOtherOffset.Outcome);
        Assert.Equal(due.UtcDateTime, sameOtherOffset.Value!.DueDate);
    }

    [Fact]
    public async Task Update_ChangingToADifferentPastInstant_OrFromNullToPast_IsRejectedOnDueDate()
    {
        var userId = await _database.AddUserAsync();
        var withDue = (await CreateAsync(userId, new CreateTaskRequest("a", null, null, null, Start.AddHours(1)))).Value!;
        var withoutDue = (await CreateAsync(userId, new CreateTaskRequest("b", null, null, null, null))).Value!;
        _time.Advance(TimeSpan.FromDays(2));

        var differentPast = await UpdateAsync(userId, withDue.Id, UpdateFrom(withDue, Start.AddHours(2)));
        var nullToPast = await UpdateAsync(userId, withoutDue.Id, UpdateFrom(withoutDue, Start));

        foreach (var result in new[] { differentPast, nullToPast })
        {
            Assert.Equal(ServiceOutcome.ValidationFailed, result.Outcome);
            Assert.Equal([DueDateRules.MustBeInFutureMessage], result.Errors!.ToDictionary()["dueDate"]);
        }

        var stored = await WithServiceAsync(s => s.GetAsync(userId, withDue.Id, CancellationToken.None));
        Assert.Equal(withDue.DueDate, stored!.DueDate);
    }

    [Fact]
    public async Task Update_OverdueTask_ClearingOrMovingToFuture_IsAllowed()
    {
        var userId = await _database.AddUserAsync();
        var task = (await CreateAsync(userId, new CreateTaskRequest("t", null, null, null, Start.AddHours(1)))).Value!;
        _time.Advance(TimeSpan.FromDays(2));

        var cleared = await UpdateAsync(userId, task.Id, UpdateFrom(task, null));
        var future = await UpdateAsync(userId, task.Id, UpdateFrom(task, _time.GetUtcNow().AddDays(1)));

        Assert.Equal(ServiceOutcome.Success, cleared.Outcome);
        Assert.Null(cleared.Value!.DueDate);
        Assert.Equal(ServiceOutcome.Success, future.Outcome);
    }

    [Fact]
    public async Task Delete_RemovesTheTask_ThenNotFound()
    {
        var userId = await _database.AddUserAsync();
        var task = (await CreateAsync(userId, new CreateTaskRequest("t", null, null, null, null))).Value!;

        Assert.True(await WithServiceAsync(s => s.DeleteAsync(userId, task.Id, CancellationToken.None)));
        Assert.False(await WithServiceAsync(s => s.DeleteAsync(userId, task.Id, CancellationToken.None)));
        await using var db = _database.CreateContext();
        Assert.Equal(0, await db.Tasks.CountAsync());
    }
}
