namespace TaskManager.Api.Entities;

/// <summary>Task status. JSON: "todo" | "inProgress" | "done". Stored as a string.</summary>
public enum TaskItemStatus
{
    Todo,
    InProgress,
    Done,
}
