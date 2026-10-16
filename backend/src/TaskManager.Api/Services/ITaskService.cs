using TaskManager.Api.Dtos.Tasks;

namespace TaskManager.Api.Services;

/// <summary>Task operations. Every method is scoped to <paramref name="userId"/>; other users' tasks behave as not found.</summary>
public interface ITaskService
{
    Task<IReadOnlyList<TaskDto>> ListAsync(Guid userId, TaskListQuery query, CancellationToken cancellationToken);

    Task<TaskDto?> GetAsync(Guid userId, Guid taskId, CancellationToken cancellationToken);

    Task<ServiceResult<TaskDto>> CreateAsync(Guid userId, CreateTaskRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<TaskDto>> UpdateAsync(Guid userId, Guid taskId, UpdateTaskRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid userId, Guid taskId, CancellationToken cancellationToken);
}
