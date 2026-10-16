using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Data;
using TaskManager.Api.Dtos.Tasks;
using TaskManager.Api.Entities;
using TaskManager.Api.Validation;

namespace TaskManager.Api.Services;

/// <summary>Builds the list query: ownership, filters, search and sorting (SPEC FR-T6 to FR-T8). LINQ only.</summary>
public static class TaskQuery
{
    public static IQueryable<TaskItem> Apply(IQueryable<TaskItem> tasks, Guid userId, TaskListQuery query, bool isSqlServer)
    {
        var filtered = Filter(tasks.Where(t => t.UserId == userId), query, isSqlServer);
        return Sort(filtered, query.SortBy, query.SortDir);
    }

    public static IQueryable<TaskItem> Filter(IQueryable<TaskItem> tasks, TaskListQuery query, bool isSqlServer)
    {
        if (query.Status is { } status)
        {
            tasks = tasks.Where(t => t.Status == status);
        }

        if (query.Priority is { } priority)
        {
            tasks = tasks.Where(t => t.Priority == priority);
        }

        var search = FieldRules.Trim(query.Search);
        if (!string.IsNullOrEmpty(search))
        {
            var pattern = LikePattern.Contains(search, isSqlServer);
            tasks = tasks.Where(t =>
                EF.Functions.Like(t.Title, pattern, LikePattern.EscapeCharacter)
                || (t.Description != null && EF.Functions.Like(t.Description, pattern, LikePattern.EscapeCharacter)));
        }

        return tasks;
    }

    /// <summary>Primary key per <paramref name="sortBy"/>, then <c>createdAt desc</c>, then <c>id asc</c>.</summary>
    public static IQueryable<TaskItem> Sort(IQueryable<TaskItem> tasks, TaskSortBy sortBy, SortDirection direction)
    {
        var ascending = direction == SortDirection.Asc;
        IOrderedQueryable<TaskItem> ordered = sortBy switch
        {
            TaskSortBy.DueDate => ascending
                // Tasks with no due date always come last, whichever direction is used.
                ? tasks.OrderBy(t => t.DueDate == null ? 1 : 0).ThenBy(t => t.DueDate)
                : tasks.OrderBy(t => t.DueDate == null ? 1 : 0).ThenByDescending(t => t.DueDate),
            TaskSortBy.Priority => ascending ? tasks.OrderBy(t => t.Priority) : tasks.OrderByDescending(t => t.Priority),
            TaskSortBy.Title => ascending ? tasks.OrderBy(t => t.Title) : tasks.OrderByDescending(t => t.Title),
            _ => ascending ? tasks.OrderBy(t => t.CreatedAt) : tasks.OrderByDescending(t => t.CreatedAt),
        };

        return ordered.ThenByDescending(t => t.CreatedAt).ThenBy(t => t.Id);
    }
}
