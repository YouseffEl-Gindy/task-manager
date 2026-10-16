using System.Net;
using System.Text.Json;
using TaskManager.Api.IntegrationTests.Infrastructure;

namespace TaskManager.Api.IntegrationTests;

/// <summary>A factory with <c>OpenApi:Enabled=true</c>, as in <c>appsettings.Development.json</c>.</summary>
public sealed class OpenApiEnabledFactory : TaskManagerApiFactory
{
    public OpenApiEnabledFactory()
        : base(new Dictionary<string, string?> { ["OpenApi:Enabled"] = "true" })
    {
    }
}

public sealed class OpenApiTests : IClassFixture<OpenApiEnabledFactory>
{
    private readonly OpenApiEnabledFactory _factory;

    public OpenApiTests(OpenApiEnabledFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Document_ListsEveryApiPath()
    {
        using var doc = await GetDocumentAsync();
        var paths = doc.RootElement.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToList();

        Assert.Equal(
            [
                "/api/v1/account/change-password", "/api/v1/account/delete", "/api/v1/account/profile",
                "/api/v1/auth/login", "/api/v1/auth/logout", "/api/v1/auth/me", "/api/v1/auth/register",
                "/api/v1/tasks", "/api/v1/tasks/{id}",
            ],
            paths.Order());
    }

    [Fact]
    public async Task Document_EnumsAreCamelCaseStrings()
    {
        using var doc = await GetDocumentAsync();
        var status = Schema(doc, "TaskItemStatus").GetProperty("enum").EnumerateArray().Select(e => e.GetString());

        Assert.Equal(["todo", "inProgress", "done"], status);
    }

    [Fact]
    public async Task Document_RequestRequiredFields_MatchContract()
    {
        using var doc = await GetDocumentAsync();

        Assert.Equal(["title"], Required(doc, "CreateTaskRequest"));
        Assert.Equal(["description", "dueDate", "priority", "status", "title"], Required(doc, "UpdateTaskRequest").Order());
    }

    [Fact]
    public async Task Document_QueryParametersAreCamelCase_AndErrorsUseApiError()
    {
        using var doc = await GetDocumentAsync();
        var list = doc.RootElement.GetProperty("paths").GetProperty("/api/v1/tasks").GetProperty("get");

        var names = list.GetProperty("parameters").EnumerateArray().Select(p => p.GetProperty("name").GetString());
        Assert.Equal(["priority", "search", "sortBy", "sortDir", "status"], names.Order());

        var badRequest = list.GetProperty("responses").GetProperty("400").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString();
        Assert.Equal("#/components/schemas/ApiError", badRequest);
    }

    [Fact]
    public async Task SwaggerUi_IsServed()
    {
        using var client = _factory.CreateApiClient();

        var response = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<JsonDocument> GetDocumentAsync()
    {
        using var client = _factory.CreateApiClient();
        var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static JsonElement Schema(JsonDocument doc, string name) =>
        doc.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(name);

    private static List<string?> Required(JsonDocument doc, string name) =>
        Schema(doc, name).GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToList();
}

/// <summary>Without <c>OpenApi:Enabled</c> (production, and the default test setup) neither endpoint exists.</summary>
public sealed class OpenApiDisabledTests : IClassFixture<TaskManagerApiFactory>
{
    private readonly TaskManagerApiFactory _factory;

    public OpenApiDisabledTests(TaskManagerApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/swagger/index.html")]
    public async Task DocsEndpoints_AreNotFound(string path)
    {
        using var client = _factory.CreateApiClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
