using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskManager.Api.Dtos.Tasks;
using TaskManager.Api.IntegrationTests.Infrastructure;
using TaskManager.Api.Services;
using static TaskManager.Api.IntegrationTests.Infrastructure.ApiTestHelpers;

namespace TaskManager.Api.IntegrationTests;

/// <summary>CSRF header, ApiError for every error source, health.</summary>
public sealed class CsrfAndErrorShapeTests : IClassFixture<TaskManagerApiFactory>
{
    private readonly TaskManagerApiFactory _factory;

    public CsrfAndErrorShapeTests(TaskManagerApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AC_CSRF_1_UnsafeRequestsWithoutHeader_Are403_AndChangeNothing()
    {
        using var noHeader = _factory.CreateApiClient(withCsrfHeader: false);
        var email = NewEmail();

        var register = await noHeader.PostAsJsonAsync("/api/v1/auth/register", new { displayName = "x", email, password = Password }, ApiJson);
        await AssertApiErrorAsync(register, HttpStatusCode.Forbidden, "csrf_failed");
        Assert.False(await _factory.WithDbAsync(db => db.Users.AnyAsync(u => u.Email == email)));

        await AssertApiErrorAsync(await LoginAsync(noHeader, email), HttpStatusCode.Forbidden, "csrf_failed");

        // A logged-in session without the header can't change tasks either.
        using var withHeader = _factory.CreateApiClient();
        await RegisterAsync(withHeader);
        var task = await CreateTaskAsync(withHeader, new { title = "keep me" });
        var cookieOnly = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/tasks/{task.Id}");
        withHeader.DefaultRequestHeaders.Remove("X-CSRF-Protection");
        await AssertApiErrorAsync(await withHeader.SendAsync(cookieOnly), HttpStatusCode.Forbidden, "csrf_failed");

        // GET without the header works.
        Assert.Equal(HttpStatusCode.OK, (await withHeader.GetAsync($"/api/v1/tasks/{task.Id}")).StatusCode);
    }

    [Fact]
    public async Task AC_ERR_1_RoutingErrors_UseApiError()
    {
        using var client = _factory.CreateApiClient();
        await RegisterAsync(client);

        await AssertApiErrorAsync(await client.GetAsync("/api/v1/does-not-exist"), HttpStatusCode.NotFound, "not_found");
        await AssertApiErrorAsync(await client.GetAsync("/api/v1/tasks/not-a-guid"), HttpStatusCode.NotFound, "not_found");
        await AssertApiErrorAsync(
            await client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, "/api/v1/tasks")),
            HttpStatusCode.MethodNotAllowed,
            "bad_request");
        await AssertApiErrorAsync(
            await client.PostAsync("/api/v1/tasks", new StringContent("title=x", Encoding.UTF8, "text/plain")),
            HttpStatusCode.UnsupportedMediaType,
            "bad_request");
    }

    [Fact]
    public async Task AC_ERR_2_MalformedJson_Is400ValidationFailed()
    {
        using var client = _factory.CreateApiClient();
        await RegisterAsync(client);

        var response = await client.PostAsync("/api/v1/tasks", new StringContent("{\"title\": ", Encoding.UTF8, "application/json"));
        await AssertApiErrorAsync(response, HttpStatusCode.BadRequest, "validation_failed");

        var notAnObject = await client.PostAsync("/api/v1/tasks", new StringContent("[1,2]", Encoding.UTF8, "application/json"));
        var error = await AssertApiErrorAsync(notAnObject, HttpStatusCode.BadRequest, "validation_failed");
        Assert.Equal(["body"], ErrorKeys(error));
    }

    [Fact]
    public async Task AC_ERR_3_UnhandledException_Is500InternalError_WithoutDetails()
    {
        using var failing = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped<ITaskService, ThrowingTaskService>()));
        using var client = failing.CreateClient(new() { BaseAddress = new Uri(TaskManagerApiFactory.BaseAddress) });
        client.DefaultRequestHeaders.Add("X-CSRF-Protection", "1");
        await RegisterAsync(client);

        var response = await client.GetAsync("/api/v1/tasks");

        var error = await AssertApiErrorAsync(response, HttpStatusCode.InternalServerError, "internal_error");
        var body = error.GetRawText();
        Assert.DoesNotContain("Boom", body);
        Assert.DoesNotContain("ThrowingTaskService", body);
    }

    [Fact]
    public async Task AC_HEALTH_1_Health_Returns200PlainText()
    {
        using var client = _factory.CreateApiClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    private sealed class ThrowingTaskService : ITaskService
    {
        public Task<IReadOnlyList<TaskDto>> ListAsync(Guid userId, TaskListQuery query, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Boom: secret detail");

        public Task<TaskDto?> GetAsync(Guid userId, Guid taskId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ServiceResult<TaskDto>> CreateAsync(Guid userId, CreateTaskRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ServiceResult<TaskDto>> UpdateAsync(Guid userId, Guid taskId, UpdateTaskRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> DeleteAsync(Guid userId, Guid taskId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
