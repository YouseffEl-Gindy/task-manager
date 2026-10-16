using TaskManager.Api.Entities;

namespace TaskManager.Api.Dtos.Tasks;

// Bound with [FromQuery]. Query keys are camelCase (binding is case-insensitive).
public sealed record TaskListQuery
{
    public TaskItemStatus? Status { get; init; }

    public TaskItemPriority? Priority { get; init; }

    public string? Search { get; init; }

    public TaskSortBy SortBy { get; init; } = TaskSortBy.CreatedAt;

    public SortDirection SortDir { get; init; } = SortDirection.Desc;
}
