using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TaskManager.Api.Dtos.Tasks;
using TaskManager.Api.IntegrationTests.Infrastructure;
using TaskManager.Api.Services;
using static TaskManager.Api.IntegrationTests.Infrastructure.ApiTestHelpers;

namespace TaskManager.Api.IntegrationTests;

/// <summary>
/// SPEC 12 contract tests: real responses match docs/API.md and frontend/types/api.ts
/// (exact camelCase keys, string enums, UTC "Z" timestamps, plain array list, ApiError shape, relative Location, nulls written).
/// </summary>
public sealed partial class ContractTests : IClassFixture<TaskManagerApiFactory>
{
    // API.md section 6, field-by-field mapping.
    private static readonly string[] UserDtoKeys = ["id", "email", "displayName", "createdAt"];
    private static readonly string[] TaskDtoKeys = ["id", "title", "description", "status", "priority", "dueDate", "createdAt", "updatedAt"];
    private static readonly string[] ApiErrorKeys = ["status", "code", "message", "errors", "traceId"];

    // API.md section 3, default messages per code.
    private static readonly Dictionary<string, string> DefaultMessages = new()
    {
        ["validation_failed"] = "One or more fields are invalid.",
        ["bad_request"] = "The request could not be processed.",
        ["invalid_credentials"] = "Email or password is incorrect.",
        ["unauthorized"] = "Authentication is required.",
        ["csrf_failed"] = "Missing or invalid CSRF header.",
        ["not_found"] = "The requested resource was not found.",
        ["email_taken"] = "An account with this email already exists.",
        ["rate_limited"] = "Too many requests. Please try again later.",
        ["internal_error"] = "An unexpected error occurred.",
    };

    private readonly TaskManagerApiFactory _factory;

    public ContractTests(TaskManagerApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Contract_FrontendTypes_MatchApiMd()
    {
        Assert.Equal(UserDtoKeys, FrontendApiTypes.InterfaceKeys("UserDto"));
        Assert.Equal(TaskDtoKeys, FrontendApiTypes.InterfaceKeys("TaskDto"));
        Assert.Equal(ApiErrorKeys, FrontendApiTypes.InterfaceKeys("ApiError"));
        Assert.Equal(["todo", "inProgress", "done"], FrontendApiTypes.UnionValues("TaskItemStatus"));
        Assert.Equal(["low", "medium", "high"], FrontendApiTypes.UnionValues("TaskItemPriority"));
        Assert.Equal(DefaultMessages.Keys.Order(), FrontendApiTypes.UnionValues("ApiErrorCode").Order());
    }

    [Fact]
    public async Task Contract_UserDto_FromRegisterLoginMeAndProfile_HasExactKeys_LowercaseId_UtcZ()
    {
        using var client = _factory.CreateApiClient();
        var email = NewEmail();

        var responses = new List<HttpResponseMessage>
        {
            await client.PostAsJsonAsync("/api/v1/auth/register", new { displayName = "Contract", email, password = Password }, ApiJson),
            await LoginAsync(client, email),
            await client.GetAsync("/api/v1/auth/me"),
            await client.PutAsJsonAsync("/api/v1/account/profile", new { displayName = "Contract 2" }, ApiJson),
        };

        foreach (var response in responses)
        {
            Assert.True(response.IsSuccessStatusCode, $"{response.RequestMessage?.RequestUri}: {response.StatusCode}");
            var user = await ReadJsonAsync(response);
            AssertExactKeys(user, UserDtoKeys, FrontendApiTypes.InterfaceKeys("UserDto"));
            AssertLowercaseGuid(user.GetProperty("id"));
            Assert.Equal(email, user.GetProperty("email").GetString());
            Assert.Equal(JsonValueKind.String, user.GetProperty("displayName").ValueKind);
            AssertUtcTimestamp(user.GetProperty("createdAt"));
        }
    }

    [Fact]
    public async Task Contract_TaskDto_FromEveryTaskEndpoint_HasExactKeys_StringEnums_UtcZ_NullsWritten()
    {
        using var client = _factory.CreateApiClient();
        await RegisterAsync(client);
        var statuses = FrontendApiTypes.UnionValues("TaskItemStatus");
        var priorities = FrontendApiTypes.UnionValues("TaskItemPriority");

        // POST with only a title: nullable fields must be written as null, not omitted.
        var minimal = await client.PostAsJsonAsync("/api/v1/tasks", new { title = "minimal" }, ApiJson);
        Assert.Equal(HttpStatusCode.Created, minimal.StatusCode);
        var minimalTask = await ReadJsonAsync(minimal);
        AssertTaskDto(minimalTask, statuses, priorities);
        Assert.Equal(JsonValueKind.Null, minimalTask.GetProperty("description").ValueKind);
        Assert.Equal(JsonValueKind.Null, minimalTask.GetProperty("dueDate").ValueKind);
        Assert.Equal("todo", minimalTask.GetProperty("status").GetString());
        Assert.Equal("medium", minimalTask.GetProperty("priority").GetString());

        // Location is the relative /api/v1/tasks/{id}, with the same lowercase id as the body.
        var id = minimalTask.GetProperty("id").GetString();
        Assert.NotNull(minimal.Headers.Location);
        Assert.False(minimal.Headers.Location!.IsAbsoluteUri);
        Assert.Equal($"/api/v1/tasks/{id}", minimal.Headers.Location.OriginalString);

        // Every enum value round-trips as its camelCase string.
        var due = _factory.Time.GetUtcNow().AddDays(1).ToOffset(TimeSpan.FromHours(2)).ToString("yyyy-MM-ddTHH:mm:ss.fffzzz");
        for (var i = 0; i < statuses.Count; i++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/v1/tasks",
                new { title = $"task {i}", description = "d", status = statuses[i], priority = priorities[i], dueDate = due },
                ApiJson);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var task = await ReadJsonAsync(response);
            AssertTaskDto(task, statuses, priorities);
            Assert.Equal(statuses[i], task.GetProperty("status").GetString());
            Assert.Equal(priorities[i], task.GetProperty("priority").GetString());
            Assert.Matches(WholeSecondUtc(), task.GetProperty("dueDate").GetString()!);
        }

        // GET by id, PUT and the list return the same shape.
        var get = await client.GetAsync($"/api/v1/tasks/{id}");
        AssertTaskDto(await ReadJsonAsync(get), statuses, priorities);

        var put = await client.PutAsJsonAsync(
            $"/api/v1/tasks/{id}",
            new { title = "minimal", description = (string?)null, status = "inProgress", priority = "high", dueDate = (string?)null },
            ApiJson);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var updated = await ReadJsonAsync(put);
        AssertTaskDto(updated, statuses, priorities);
        Assert.Equal(JsonValueKind.Null, updated.GetProperty("description").ValueKind);
        Assert.Equal(JsonValueKind.Null, updated.GetProperty("dueDate").ValueKind);

        var list = await ReadJsonAsync(await client.GetAsync("/api/v1/tasks"));
        Assert.Equal(JsonValueKind.Array, list.ValueKind);
        Assert.Equal(4, list.GetArrayLength());
        foreach (var task in list.EnumerateArray())
        {
            AssertTaskDto(task, statuses, priorities);
        }
    }

    [Fact]
    public async Task Contract_GetTasks_IsAPlainJsonArray_EvenWhenEmptyOrFiltered()
    {
        using var client = _factory.CreateApiClient();
        await RegisterAsync(client);

        var empty = await client.GetAsync("/api/v1/tasks");
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        Assert.Equal("application/json", empty.Content.Headers.ContentType?.MediaType);
        Assert.Equal("[]", await empty.Content.ReadAsStringAsync());

        await CreateTaskAsync(client, new { title = "one" });
        var filtered = await ReadJsonAsync(await client.GetAsync("/api/v1/tasks?status=todo&sortBy=title&sortDir=asc&search=one"));
        Assert.Equal(JsonValueKind.Array, filtered.ValueKind); // not wrapped in { items, total } or similar
        Assert.Equal(1, filtered.GetArrayLength());
    }

    [Fact]
    public async Task Contract_ApiError_HasExactKeys_ForEveryErrorTheBackendProduces()
    {
        using var client = _factory.CreateApiClient();
        var email = NewEmail();
        await RegisterAsync(client, email);
        using var anonymous = _factory.CreateApiClient();
        using var noCsrf = _factory.CreateApiClient(withCsrfHeader: false);

        var cases = new List<(string Name, HttpResponseMessage Response, HttpStatusCode Status, string Code)>
        {
            ("register validation", await anonymous.PostAsJsonAsync("/api/v1/auth/register", new { displayName = "", email = "nope", password = "short" }, ApiJson), HttpStatusCode.BadRequest, "validation_failed"),
            ("malformed json", await client.PostAsync("/api/v1/tasks", new StringContent("{\"title\":", Encoding.UTF8, "application/json")), HttpStatusCode.BadRequest, "validation_failed"),
            ("query validation", await client.GetAsync("/api/v1/tasks?status=1"), HttpStatusCode.BadRequest, "validation_failed"),
            ("unauthorized", await anonymous.GetAsync("/api/v1/tasks"), HttpStatusCode.Unauthorized, "unauthorized"),
            ("invalid credentials", await LoginAsync(anonymous, email, "wrongpass1"), HttpStatusCode.Unauthorized, "invalid_credentials"),
            ("csrf", await noCsrf.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }, ApiJson), HttpStatusCode.Forbidden, "csrf_failed"),
            ("task not found", await client.GetAsync($"/api/v1/tasks/{Guid.NewGuid()}"), HttpStatusCode.NotFound, "not_found"),
            ("route not found", await client.GetAsync("/api/v1/nothing-here"), HttpStatusCode.NotFound, "not_found"),
            ("method not allowed", await client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, "/api/v1/tasks")), HttpStatusCode.MethodNotAllowed, "bad_request"),
            ("unsupported media type", await client.PostAsync("/api/v1/tasks", new StringContent("x", Encoding.UTF8, "text/plain")), HttpStatusCode.UnsupportedMediaType, "bad_request"),
            ("email taken", await anonymous.PostAsJsonAsync("/api/v1/auth/register", new { displayName = "x", email, password = Password }, ApiJson), HttpStatusCode.Conflict, "email_taken"),
        };

        foreach (var (name, response, status, code) in cases)
        {
            var error = await AssertApiErrorContractAsync(response, status, code, name);
            if (code == "validation_failed")
            {
                foreach (var key in error.GetProperty("errors").EnumerateObject())
                {
                    Assert.Matches(CamelCaseKey(), key.Name);
                    Assert.Equal(JsonValueKind.Array, key.Value.ValueKind);
                    Assert.All(key.Value.EnumerateArray(), m => Assert.Equal(JsonValueKind.String, m.ValueKind));
                }
            }
        }

        // Field keys are the camelCase request property names.
        var register = await ReadJsonAsync(cases[0].Response);
        Assert.Equal(["displayName", "email", "password"], ErrorKeys(register));
    }

    [Fact]
    public async Task Contract_ApiError_ForUnhandledException_HasExactKeys()
    {
        using var failing = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped<ITaskService, ThrowingTaskService>()));
        using var client = failing.CreateClient(new() { BaseAddress = new Uri(TaskManagerApiFactory.BaseAddress) });
        client.DefaultRequestHeaders.Add("X-CSRF-Protection", "1");
        await RegisterAsync(client);

        await AssertApiErrorContractAsync(await client.GetAsync("/api/v1/tasks"), HttpStatusCode.InternalServerError, "internal_error", "500");
    }

    internal static async Task<JsonElement> AssertApiErrorContractAsync(HttpResponseMessage response, HttpStatusCode status, string code, string name)
    {
        Assert.True(status == response.StatusCode, $"{name}: expected {(int)status}, got {(int)response.StatusCode}");
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Null(response.Headers.Location);

        var error = await ReadJsonAsync(response);
        AssertExactKeys(error, ApiErrorKeys, FrontendApiTypes.InterfaceKeys("ApiError"), name);
        Assert.Equal(JsonValueKind.Number, error.GetProperty("status").ValueKind);
        Assert.Equal((int)status, error.GetProperty("status").GetInt32());
        Assert.Equal(code, error.GetProperty("code").GetString());
        Assert.Contains(code, FrontendApiTypes.UnionValues("ApiErrorCode"));
        Assert.Equal(DefaultMessages[code], error.GetProperty("message").GetString());
        Assert.Equal(code == "validation_failed" ? JsonValueKind.Object : JsonValueKind.Null, error.GetProperty("errors").ValueKind);
        Assert.Equal(JsonValueKind.String, error.GetProperty("traceId").ValueKind);
        Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("traceId").GetString()), $"{name}: traceId is empty");
        return error;
    }

    internal static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static void AssertTaskDto(JsonElement task, IReadOnlyList<string> statuses, IReadOnlyList<string> priorities)
    {
        AssertExactKeys(task, TaskDtoKeys, FrontendApiTypes.InterfaceKeys("TaskDto"));
        AssertLowercaseGuid(task.GetProperty("id"));
        Assert.Equal(JsonValueKind.String, task.GetProperty("title").ValueKind);
        Assert.Contains(task.GetProperty("description").ValueKind, new[] { JsonValueKind.String, JsonValueKind.Null });

        // Enums are strings from the shared union, never numbers.
        Assert.Equal(JsonValueKind.String, task.GetProperty("status").ValueKind);
        Assert.Contains(task.GetProperty("status").GetString(), statuses);
        Assert.Equal(JsonValueKind.String, task.GetProperty("priority").ValueKind);
        Assert.Contains(task.GetProperty("priority").GetString(), priorities);

        var dueDate = task.GetProperty("dueDate");
        if (dueDate.ValueKind != JsonValueKind.Null)
        {
            Assert.Equal(JsonValueKind.String, dueDate.ValueKind);
            Assert.Matches(WholeSecondUtc(), dueDate.GetString()!);
        }

        AssertUtcTimestamp(task.GetProperty("createdAt"));
        AssertUtcTimestamp(task.GetProperty("updatedAt"));
    }

    private static void AssertExactKeys(JsonElement element, string[] apiMdKeys, IReadOnlyList<string> tsKeys, string context = "")
    {
        Assert.Equal(JsonValueKind.Object, element.ValueKind);
        var actual = element.EnumerateObject().Select(p => p.Name).ToList();
        Assert.True(actual.Count == actual.Distinct(StringComparer.Ordinal).Count(), $"{context}: duplicate keys");
        Assert.Equal(apiMdKeys.Order(StringComparer.Ordinal), actual.Order(StringComparer.Ordinal));
        Assert.Equal(tsKeys.Order(StringComparer.Ordinal), actual.Order(StringComparer.Ordinal));
    }

    private static void AssertLowercaseGuid(JsonElement id)
    {
        Assert.Equal(JsonValueKind.String, id.ValueKind);
        Assert.Matches(LowercaseGuid(), id.GetString()!);
    }

    private static void AssertUtcTimestamp(JsonElement value)
    {
        Assert.Equal(JsonValueKind.String, value.ValueKind);
        var text = value.GetString()!;
        Assert.Matches(UtcTimestamp(), text);
        Assert.True(DateTimeOffset.TryParse(text, out var parsed), text);
        Assert.Equal(TimeSpan.Zero, parsed.Offset);
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?Z$")]
    private static partial Regex UtcTimestamp();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$")]
    private static partial Regex WholeSecondUtc();

    [GeneratedRegex("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$")]
    private static partial Regex LowercaseGuid();

    [GeneratedRegex("^[a-z][A-Za-z0-9]*$")]
    private static partial Regex CamelCaseKey();

    private sealed class ThrowingTaskService : ITaskService
    {
        public Task<IReadOnlyList<TaskDto>> ListAsync(Guid userId, TaskListQuery query, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("contract test failure");

        public Task<TaskDto?> GetAsync(Guid userId, Guid taskId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ServiceResult<TaskDto>> CreateAsync(Guid userId, CreateTaskRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ServiceResult<TaskDto>> UpdateAsync(Guid userId, Guid taskId, UpdateTaskRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> DeleteAsync(Guid userId, Guid taskId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}

/// <summary>Contract shape of the 429 response; uses the production limit in its own factory so it can't affect other classes.</summary>
public sealed class RateLimitContractTests : IClassFixture<RateLimitedApiFactory>
{
    private readonly RateLimitedApiFactory _factory;

    public RateLimitContractTests(RateLimitedApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Contract_ApiError_For429_HasExactKeys_AndRetryAfterInSeconds()
    {
        using var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "192.0.2.77");

        HttpResponseMessage response;
        var attempts = 0;
        do
        {
            response = await LoginAsync(client, NewEmail(), "wrongpass1");
            attempts++;
        }
        while (response.StatusCode != HttpStatusCode.TooManyRequests && attempts <= 11);

        Assert.Equal(11, attempts);
        await ContractTests.AssertApiErrorContractAsync(response, HttpStatusCode.TooManyRequests, "rate_limited", "429");
        var retryAfter = Assert.Single(response.Headers.GetValues("Retry-After"));
        Assert.True(int.TryParse(retryAfter, out var seconds) && seconds > 0, $"Retry-After should be whole seconds, was '{retryAfter}'");
    }
}
