using TaskManager.Api.Dtos.Common;
using TaskManager.Api.Middleware;

namespace TaskManager.Api.Auth;

/// <summary>
/// Rejects POST/PUT/PATCH/DELETE under <c>/api/</c> without <c>X-CSRF-Protection: 1</c> with <c>403 csrf_failed</c>.
/// Runs before authentication and model binding, so nothing changes (SPEC FR-A6).
/// </summary>
public sealed class CsrfHeaderMiddleware
{
    public const string HeaderName = "X-CSRF-Protection";
    public const string HeaderValue = "1";

    private readonly RequestDelegate _next;

    public CsrfHeaderMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (RequiresHeader(context.Request) && !HasValidHeader(context.Request))
        {
            await ApiErrorResponses.WriteAsync(context, StatusCodes.Status403Forbidden, ApiErrorCodes.CsrfFailed);
            return;
        }

        await _next(context);
    }

    public static bool RequiresHeader(HttpRequest request) =>
        request.Path.StartsWithSegments("/api")
        && (HttpMethods.IsPost(request.Method)
            || HttpMethods.IsPut(request.Method)
            || HttpMethods.IsPatch(request.Method)
            || HttpMethods.IsDelete(request.Method));

    public static bool HasValidHeader(HttpRequest request) =>
        request.Headers.TryGetValue(HeaderName, out var values)
        && values.Count == 1
        && string.Equals(values[0], HeaderValue, StringComparison.Ordinal);
}
