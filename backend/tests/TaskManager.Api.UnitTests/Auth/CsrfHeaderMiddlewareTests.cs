using System.Text.Json;
using Microsoft.AspNetCore.Http;
using TaskManager.Api.Auth;

namespace TaskManager.Api.UnitTests.Auth;

/// <summary>SPEC FR-A6 / AC-CSRF-1.</summary>
public sealed class CsrfHeaderMiddlewareTests
{
    private static async Task<(bool NextCalled, HttpContext Context)> InvokeAsync(string method, string path, string? header)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        if (header is not null)
        {
            context.Request.Headers[CsrfHeaderMiddleware.HeaderName] = header;
        }

        var nextCalled = false;
        var middleware = new CsrfHeaderMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        await middleware.InvokeAsync(context);
        return (nextCalled, context);
    }

    [Theory]
    [InlineData("POST", "/api/v1/auth/login")]
    [InlineData("POST", "/api/v1/auth/register")]
    [InlineData("PUT", "/api/v1/tasks/3f2b8c1e-9a4d-4c7e-8f61-2d5a7b9c0e14")]
    [InlineData("PATCH", "/api/v1/tasks")]
    [InlineData("DELETE", "/api/v1/tasks/3f2b8c1e-9a4d-4c7e-8f61-2d5a7b9c0e14")]
    public async Task UnsafeApiRequestWithoutHeader_Gets403CsrfFailed(string method, string path)
    {
        var (nextCalled, context) = await InvokeAsync(method, path, header: null);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("csrf_failed", json.RootElement.GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("errors").ValueKind);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("true")]
    [InlineData("")]
    public async Task WrongHeaderValue_IsRejected(string value)
    {
        var (nextCalled, context) = await InvokeAsync("POST", "/api/v1/tasks", value);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/v1/tasks", "1")]
    [InlineData("GET", "/api/v1/tasks", null)]
    [InlineData("HEAD", "/api/v1/tasks", null)]
    [InlineData("POST", "/health", null)]
    [InlineData("POST", "/apix", null)]
    public async Task SafeMethodsCorrectHeaderOrNonApiPaths_PassThrough(string method, string path, string? header)
    {
        var (nextCalled, _) = await InvokeAsync(method, path, header);

        Assert.True(nextCalled);
    }
}
