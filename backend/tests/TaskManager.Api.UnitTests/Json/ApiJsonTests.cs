using System.Text.Json;
using TaskManager.Api.Dtos.Common;
using TaskManager.Api.Dtos.Tasks;
using TaskManager.Api.Entities;
using TaskManager.Api.Json;

namespace TaskManager.Api.UnitTests.Json;

/// <summary>JSON settings from API.md section 6 and the strict DateTimeOffset converter (SPEC 5.1, AC-DUE-3/4).</summary>
public sealed class ApiJsonTests
{
    private static readonly JsonSerializerOptions Options = ApiJsonOptions.Default;

    [Theory]
    [InlineData("2026-10-12T17:00:00+02:00", "2026-10-12T15:00:00Z")]
    [InlineData("2026-10-12T15:00:00Z", "2026-10-12T15:00:00Z")]
    [InlineData("2026-10-12T15:00:00.789Z", "2026-10-12T15:00:00.789Z")]
    [InlineData("2026-10-12T10:00:00-05:00", "2026-10-12T15:00:00Z")]
    [InlineData("2026-10-12T15:00Z", "2026-10-12T15:00:00Z")]
    public void StrictConverter_AcceptsIsoWithOffsetOrZ(string input, string expectedUtc)
    {
        Assert.True(StrictDateTimeOffsetConverter.TryParse(input, out var value));
        Assert.Equal(DateTimeOffset.Parse(expectedUtc), value);
    }

    [Theory]
    [InlineData("2026-10-12T15:00:00")]   // no offset
    [InlineData("2026-10-12")]            // date only
    [InlineData("2026-10-12 15:00:00Z")]  // space instead of T
    [InlineData("12/10/2026 15:00 +00:00")]
    [InlineData("2026-13-12T15:00:00Z")]  // invalid month
    [InlineData("2026-10-12T15:00:00+0200")]
    [InlineData("")]
    [InlineData("tomorrow")]
    public void StrictConverter_RejectsValuesWithoutOffsetOrMalformed(string input)
    {
        Assert.False(StrictDateTimeOffsetConverter.TryParse(input, out _));
    }

    [Fact]
    public void Deserialize_CreateTaskRequest_WithOffset()
    {
        var request = JsonSerializer.Deserialize<CreateTaskRequest>(
            """{"title":"t","status":"inProgress","priority":"high","dueDate":"2026-10-12T17:00:00+02:00","userId":"ignored"}""",
            Options)!;

        Assert.Equal(TaskItemStatus.InProgress, request.Status);
        Assert.Equal(TaskItemPriority.High, request.Priority);
        Assert.Equal(new DateTimeOffset(2026, 10, 12, 15, 0, 0, TimeSpan.Zero), request.DueDate);
    }

    [Fact]
    public void Deserialize_DueDateWithoutOffset_Throws()
    {
        var ex = Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<CreateTaskRequest>("""{"title":"t","dueDate":"2026-10-12T15:00:00"}""", Options));
        Assert.Equal("$.dueDate", ex.Path);
    }

    [Fact]
    public void Deserialize_NullDueDate_IsNull()
    {
        var request = JsonSerializer.Deserialize<CreateTaskRequest>("""{"title":"t","dueDate":null}""", Options)!;
        Assert.Null(request.DueDate);
    }

    [Theory]
    [InlineData("""{"title":"t","status":1}""")]
    [InlineData("""{"title":"t","status":"1"}""")]
    [InlineData("""{"title":"t","status":"finished"}""")]
    [InlineData("""{"title":"t","priority":2}""")]
    public void Deserialize_IntegerOrUnknownEnumValues_AreRejected(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreateTaskRequest>(json, Options));
    }

    [Fact]
    public void Deserialize_UpdateTaskRequest_MissingRequiredKeys_Throws()
    {
        var ex = Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<UpdateTaskRequest>("""{"title":"t","status":"done","priority":"low"}""", Options));
        Assert.Contains("'description'", ex.Message);
        Assert.Contains("'dueDate'", ex.Message);
    }

    [Fact]
    public void Deserialize_UpdateTaskRequest_MissingStatusAndPriority_Throws()
    {
        var ex = Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<UpdateTaskRequest>("""{"title":"t","description":null,"dueDate":null}""", Options));
        Assert.Contains("'status'", ex.Message);
        Assert.Contains("'priority'", ex.Message);
    }

    [Fact]
    public void Deserialize_UpdateTaskRequest_ExplicitNulls_AreAccepted()
    {
        var request = JsonSerializer.Deserialize<UpdateTaskRequest>(
            """{"title":"t","description":null,"status":"done","priority":"low","dueDate":null}""", Options)!;

        Assert.Null(request.Description);
        Assert.Null(request.DueDate);
        Assert.Equal(TaskItemStatus.Done, request.Status);
    }

    [Fact]
    public void Serialize_TaskDto_UsesCamelCase_StringEnums_UtcZ_AndWritesNulls()
    {
        var dto = new TaskDto(
            Guid.Parse("3F2B8C1E-9A4D-4C7E-8F61-2D5A7B9C0E14"),
            "Write report",
            null,
            TaskItemStatus.InProgress,
            TaskItemPriority.High,
            null,
            new DateTime(2026, 10, 10, 14, 35, 12, 441, DateTimeKind.Utc),
            new DateTime(2026, 10, 10, 14, 35, 12, 441, DateTimeKind.Utc));

        var json = JsonSerializer.Serialize(dto, Options);

        Assert.Equal(
            """{"id":"3f2b8c1e-9a4d-4c7e-8f61-2d5a7b9c0e14","title":"Write report","description":null,"status":"inProgress","priority":"high","dueDate":null,"createdAt":"2026-10-10T14:35:12.441Z","updatedAt":"2026-10-10T14:35:12.441Z"}""",
            json);
    }

    [Fact]
    public void Serialize_ApiError_KeepsFieldKeysAsGiven_AndWritesNullErrors()
    {
        var withErrors = JsonSerializer.Serialize(
            new ApiError(400, ApiErrorCodes.ValidationFailed, "m", new Dictionary<string, string[]> { ["dueDate"] = ["x"] }, "t"),
            Options);
        var withoutErrors = JsonSerializer.Serialize(new ApiError(404, ApiErrorCodes.NotFound, "m", null, "t"), Options);

        Assert.Equal("""{"status":400,"code":"validation_failed","message":"m","errors":{"dueDate":["x"]},"traceId":"t"}""", withErrors);
        Assert.Equal("""{"status":404,"code":"not_found","message":"m","errors":null,"traceId":"t"}""", withoutErrors);
    }
}
