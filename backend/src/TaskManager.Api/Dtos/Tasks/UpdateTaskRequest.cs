using System.Text.Json.Serialization;
using TaskManager.Api.Entities;

namespace TaskManager.Api.Dtos.Tasks;

/// <summary>
/// Full replace. Every key must be present (API.md 5.11). <c>Status</c> and <c>Priority</c> also carry
/// <see cref="JsonRequiredAttribute"/>: as non-nullable enums, a missing key would otherwise silently become
/// the default value instead of returning 400 (AC-EDIT-4). The JSON shape is unchanged.
/// </summary>
public sealed record UpdateTaskRequest(
    string Title,
    [property: JsonRequired] string? Description,        // key required, value may be null
    [property: JsonRequired] TaskItemStatus Status,
    [property: JsonRequired] TaskItemPriority Priority,
    [property: JsonRequired] DateTimeOffset? DueDate);   // key required, value may be null; stored as UTC DateTime
