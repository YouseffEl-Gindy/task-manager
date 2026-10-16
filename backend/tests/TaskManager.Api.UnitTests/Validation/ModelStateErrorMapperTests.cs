using Microsoft.AspNetCore.Mvc.ModelBinding;
using TaskManager.Api.Json;
using TaskManager.Api.Validation;

namespace TaskManager.Api.UnitTests.Validation;

/// <summary>Malformed JSON and binding errors become camelCase keys or <c>body</c> (API.md section 3 and 6).</summary>
public sealed class ModelStateErrorMapperTests
{
    private static readonly string[] BodyParameters = ["request"];

    private static IReadOnlyDictionary<string, string[]> Map(params (string Key, string Message)[] entries)
    {
        var modelState = new ModelStateDictionary();
        foreach (var (key, message) in entries)
        {
            modelState.AddModelError(key, message);
        }

        return ModelStateErrorMapper.Map(modelState, BodyParameters).ToDictionary();
    }

    [Theory]
    [InlineData("$.dueDate", "dueDate")]
    [InlineData("$.title", "title")]
    [InlineData("$.Status", "status")]
    [InlineData("$.items[0].name", "items")]
    [InlineData("$", "body")]
    [InlineData("", "body")]
    [InlineData("$['weird key']", "body")]
    public void FieldFromJsonPath_MapsToFirstSegmentOrBody(string key, string expected)
    {
        Assert.Equal(expected, ModelStateErrorMapper.FieldFromJsonPath(key));
    }

    [Theory]
    [InlineData("Status", "status")]
    [InlineData("query.SortBy", "sortBy")]
    [InlineData("sortDir", "sortDir")]
    public void FieldFromModelKey_IsCamelCaseLastSegment(string key, string expected)
    {
        Assert.Equal(expected, ModelStateErrorMapper.FieldFromModelKey(key));
    }

    [Fact]
    public void Map_MissingRequiredProperties_AreKeyedByPropertyName()
    {
        var errors = Map(("$", "JSON deserialization for type 'TaskManager.Api.Dtos.Tasks.UpdateTaskRequest' was missing required properties including: 'description', 'dueDate'."));

        Assert.Equal(["description", "dueDate"], errors.Keys.Order());
        Assert.Equal([ModelStateErrorMapper.RequiredMessage], errors["dueDate"]);
    }

    [Fact]
    public void Map_StrictDateTimeOffsetMessage_IsKept()
    {
        var errors = Map(("$.dueDate", StrictDateTimeOffsetConverter.InvalidFormatMessage + " Path: $.dueDate | LineNumber: 0 | BytePositionInLine: 40."));

        Assert.Equal([StrictDateTimeOffsetConverter.InvalidFormatMessage], errors["dueDate"]);
    }

    [Fact]
    public void Map_RawSerializerMessages_AreNotReturned()
    {
        var errors = Map(("$.status", "The JSON value could not be converted to System.Nullable`1[TaskManager.Api.Entities.TaskItemStatus]. Path: $.status"));

        Assert.Equal([ModelStateErrorMapper.InvalidValueMessage], errors["status"]);
    }

    [Fact]
    public void Map_BodyParameterOrRootErrors_UseBodyKey()
    {
        var errors = Map(("request", "The request field is required."), ("", "A non-empty request body is required."));

        Assert.Equal(["body"], errors.Keys);
        Assert.Equal([ModelStateErrorMapper.InvalidJsonMessage], errors["body"]);
    }

    [Fact]
    public void Map_QueryBindingErrors_KeepTheirMessage()
    {
        var errors = Map(("Status", "The value is not valid. Allowed values: todo, inProgress, done."));

        Assert.Equal(["The value is not valid. Allowed values: todo, inProgress, done."], errors["status"]);
    }

    [Fact]
    public void Map_EmptyModelState_FallsBackToBody()
    {
        Assert.Equal(["body"], ModelStateErrorMapper.Map(new ModelStateDictionary(), BodyParameters).ToDictionary().Keys);
    }

    [Fact]
    public void MissingRequiredProperties_OtherMessages_ReturnEmpty()
    {
        Assert.Empty(ModelStateErrorMapper.MissingRequiredProperties("'$' is an invalid start of a value."));
        Assert.Empty(ModelStateErrorMapper.MissingRequiredProperties(null));
    }
}
