namespace TaskManager.Api.Entities;

/// <summary>Task priority. JSON: "low" | "medium" | "high". Stored as an int so low &lt; medium &lt; high sorts numerically.</summary>
public enum TaskItemPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
}
