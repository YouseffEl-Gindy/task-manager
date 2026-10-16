using TaskManager.Api.Dtos.Tasks;
using TaskManager.Api.Entities;

namespace TaskManager.Api.Mapping;

public static class TaskMappings
{
    public static TaskDto ToDto(this TaskItem task) =>
        new(
            task.Id,
            task.Title,
            task.Description,
            task.Status,
            task.Priority,
            task.DueDate is { } due ? AsUtc(due) : null,
            AsUtc(task.CreatedAt),
            AsUtc(task.UpdatedAt));

    /// <summary>Every outgoing DateTime is UTC so it serializes with "Z".</summary>
    public static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
