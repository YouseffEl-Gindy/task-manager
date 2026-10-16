using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Data;
using TaskManager.Api.Dtos.Tasks;
using TaskManager.Api.Entities;
using TaskManager.Api.Mapping;
using TaskManager.Api.Validation;

namespace TaskManager.Api.Services;

/// <summary>Task CRUD. Requests reaching here have passed field validation; this applies trimming, defaults and the update due-date rule.</summary>
public sealed class TaskService : ITaskService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;

    public TaskService(AppDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<TaskDto>> ListAsync(Guid userId, TaskListQuery query, CancellationToken cancellationToken)
    {
        var tasks = await TaskQuery.Apply(_db.Tasks.AsNoTracking(), userId, query, _db.Database.IsSqlServer())
            .ToListAsync(cancellationToken);
        return tasks.Select(t => t.ToDto()).ToList();
    }

    public async Task<TaskDto?> GetAsync(Guid userId, Guid taskId, CancellationToken cancellationToken)
    {
        var task = await _db.Tasks.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId, cancellationToken);
        return task?.ToDto();
    }

    public async Task<ServiceResult<TaskDto>> CreateAsync(Guid userId, CreateTaskRequest request, CancellationToken cancellationToken)
    {
        // The session can outlive a deleted account until the security stamp is next checked.
        if (!await _db.Users.AnyAsync(u => u.Id == userId, cancellationToken))
        {
            return ServiceResult<TaskDto>.Fail(ServiceOutcome.Unauthorized);
        }

        var now = UtcNow();
        var task = new TaskItem
        {
            UserId = userId,
            Title = request.Title.Trim(),
            Description = FieldRules.NormalizeOptionalText(request.Description),
            Status = request.Status ?? TaskItemStatus.Todo,
            Priority = request.Priority ?? TaskItemPriority.Medium,
            DueDate = DueDateRules.ToUtcWholeSeconds(request.DueDate),
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.Tasks.Add(task);
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<TaskDto>.Ok(task.ToDto());
    }

    public async Task<ServiceResult<TaskDto>> UpdateAsync(Guid userId, Guid taskId, UpdateTaskRequest request, CancellationToken cancellationToken)
    {
        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId, cancellationToken);
        if (task is null)
        {
            return ServiceResult<TaskDto>.Fail(ServiceOutcome.NotFound);
        }

        var now = UtcNow();
        var newDue = DueDateRules.ToUtcWholeSeconds(request.DueDate);
        if (!DueDateRules.IsAllowedOnUpdate(newDue, task.DueDate, now))
        {
            return ServiceResult<TaskDto>.Invalid(
                ValidationErrors.Single(DueDateRules.Field, DueDateRules.MustBeInFutureMessage));
        }

        task.Title = request.Title.Trim();
        task.Description = FieldRules.NormalizeOptionalText(request.Description);
        task.Status = request.Status;
        task.Priority = request.Priority;
        task.DueDate = newDue;
        task.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<TaskDto>.Ok(task.ToDto());
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid taskId, CancellationToken cancellationToken)
    {
        var deleted = await _db.Tasks
            .Where(t => t.Id == taskId && t.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }

    private DateTime UtcNow() => _timeProvider.GetUtcNow().UtcDateTime;
}
