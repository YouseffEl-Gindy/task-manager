using TaskManager.Api.Dtos.Tasks;
using TaskManager.Api.Entities;
using TaskManager.Api.Validation;

namespace TaskManager.Api.UnitTests.Validation;

/// <summary>Query-string enums must be valid names; numeric strings are rejected (SPEC 5.1, AC-LIST-7).</summary>
public sealed class StrictEnumNamesTests
{
    [Theory]
    [InlineData(typeof(TaskItemStatus), "todo", TaskItemStatus.Todo)]
    [InlineData(typeof(TaskItemStatus), "inProgress", TaskItemStatus.InProgress)]
    [InlineData(typeof(TaskItemStatus), "DONE", TaskItemStatus.Done)]
    [InlineData(typeof(TaskItemPriority), "high", TaskItemPriority.High)]
    [InlineData(typeof(TaskSortBy), "dueDate", TaskSortBy.DueDate)]
    [InlineData(typeof(SortDirection), "asc", SortDirection.Asc)]
    public void TryParse_AcceptsDefinedNames(Type enumType, string text, object expected)
    {
        Assert.True(StrictEnumNames.TryParse(enumType, text, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData(typeof(TaskItemStatus), "1")]
    [InlineData(typeof(TaskItemStatus), "0")]
    [InlineData(typeof(TaskItemStatus), "-1")]
    [InlineData(typeof(TaskItemStatus), "in_progress")]
    [InlineData(typeof(TaskItemPriority), "urgent")]
    [InlineData(typeof(TaskSortBy), "Todo,Done")]
    [InlineData(typeof(SortDirection), "ascending")]
    public void TryParse_RejectsNumbersAndUnknownNames(Type enumType, string text)
    {
        Assert.False(StrictEnumNames.TryParse(enumType, text, out _));
    }

    [Fact]
    public void JsonNames_AreCamelCase()
    {
        Assert.Equal(["todo", "inProgress", "done"], StrictEnumNames.JsonNames(typeof(TaskItemStatus)));
        Assert.Equal(["createdAt", "dueDate", "priority", "title"], StrictEnumNames.JsonNames(typeof(TaskSortBy)));
    }
}
