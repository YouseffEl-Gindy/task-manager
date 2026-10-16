using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TaskManager.Api.Dtos.Auth;
using TaskManager.Api.Dtos.Tasks;
using TaskManager.Api.Json;

namespace TaskManager.Api.IntegrationTests.Infrastructure;

public static class ApiTestHelpers
{
    public const string Password = "correcthorse1";

    public static JsonSerializerOptions ApiJson => ApiJsonOptions.Default;

    public static string NewEmail() => $"user{Guid.NewGuid():N}@example.com";

    public static async Task<UserDto> RegisterAsync(HttpClient client, string? email = null, string password = Password)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { displayName = "Test User", email = email ?? NewEmail(), password },
            ApiJson);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<UserDto>(ApiJson))!;
    }

    public static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password = Password) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, ApiJson);

    public static async Task<TaskDto> CreateTaskAsync(HttpClient client, object body)
    {
        var response = await client.PostAsJsonAsync("/api/v1/tasks", body, ApiJson);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TaskDto>(ApiJson))!;
    }

    /// <summary>Asserts the ApiError shape (all five fields present) and returns the parsed body.</summary>
    public static async Task<JsonElement> AssertApiErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Null(response.Headers.Location);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement.Clone();
        Assert.Equal((int)status, root.GetProperty("status").GetInt32());
        Assert.Equal(code, root.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(root.GetProperty("message").GetString()));
        Assert.True(root.TryGetProperty("errors", out var errors));
        Assert.Equal(code == "validation_failed" ? JsonValueKind.Object : JsonValueKind.Null, errors.ValueKind);
        Assert.False(string.IsNullOrEmpty(root.GetProperty("traceId").GetString()));
        return root;
    }

    public static IReadOnlyList<string> ErrorKeys(JsonElement apiError) =>
        apiError.GetProperty("errors").EnumerateObject().Select(p => p.Name).Order().ToList();
}
