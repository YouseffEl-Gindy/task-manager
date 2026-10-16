using TaskManager.Api.Entities;

namespace TaskManager.Api.Dtos.Tasks;

public sealed record CreateTaskRequest(
    string Title,
    string? Description,
    TaskItemStatus? Status,     // null/missing -> Todo
    TaskItemPriority? Priority, // null/missing -> Medium
    DateTimeOffset? DueDate);   // offset required (StrictDateTimeOffsetConverter); stored as UTC DateTime
