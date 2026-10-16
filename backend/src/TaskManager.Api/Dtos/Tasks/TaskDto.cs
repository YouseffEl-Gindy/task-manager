using TaskManager.Api.Entities;

namespace TaskManager.Api.Dtos.Tasks;

public sealed record TaskDto(
    Guid Id,
    string Title,
    string? Description,
    TaskItemStatus Status,
    TaskItemPriority Priority,
    DateTime? DueDate,          // UTC, whole seconds, serialized with "Z"
    DateTime CreatedAt,         // UTC
    DateTime UpdatedAt);        // UTC
