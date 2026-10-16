using System.Diagnostics;
using System.Text.Json;
using TaskManager.Api.Dtos.Common;
using TaskManager.Api.Json;

namespace TaskManager.Api.Middleware;

/// <summary>Builds and writes <see cref="ApiError"/> bodies.</summary>
public static class ApiErrorResponses
{
    public const string JsonContentType = "application/json; charset=utf-8";

    public static string TraceIdOf(HttpContext context) => Activity.Current?.Id ?? context.TraceIdentifier;

    public static ApiError Create(
        HttpContext context,
        int status,
        string code,
        IReadOnlyDictionary<string, string[]>? errors = null,
        string? message = null) =>
        new(status, code, message ?? ApiErrorMessages.For(code), errors, TraceIdOf(context));

    public static async Task WriteAsync(
        HttpContext context,
        int status,
        string code,
        IReadOnlyDictionary<string, string[]>? errors = null,
        string? message = null)
    {
        var body = Create(context, status, code, errors, message);
        context.Response.StatusCode = status;
        context.Response.ContentType = JsonContentType;
        await JsonSerializer.SerializeAsync(context.Response.Body, body, ApiJsonOptions.Default, context.RequestAborted);
    }

    /// <summary>Maps a bare status code (routing 404/405, 415, ...) to an error code.</summary>
    public static string CodeForStatus(int status) => status switch
    {
        StatusCodes.Status401Unauthorized => ApiErrorCodes.Unauthorized,
        StatusCodes.Status404NotFound => ApiErrorCodes.NotFound,
        StatusCodes.Status429TooManyRequests => ApiErrorCodes.RateLimited,
        >= 500 => ApiErrorCodes.InternalError,
        _ => ApiErrorCodes.BadRequest,
    };

    public static bool IsApiPath(HttpContext context) => context.Request.Path.StartsWithSegments("/api");
}
