using Microsoft.AspNetCore.Identity;

namespace TaskManager.Api.Entities;

/// <summary>Application user. <c>Id</c> is left as <see cref="Guid.Empty"/> on insert so EF generates it.</summary>
public class User : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }

    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}
