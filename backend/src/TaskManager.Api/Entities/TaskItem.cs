namespace TaskManager.Api.Entities;

/// <summary>A task owned by exactly one user. All timestamps are UTC <see cref="DateTime"/>.</summary>
public class TaskItem
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public TaskItemStatus Status { get; set; } = TaskItemStatus.Todo;

    public TaskItemPriority Priority { get; set; } = TaskItemPriority.Medium;

    /// <summary>UTC, whole seconds.</summary>
    public DateTime? DueDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}
