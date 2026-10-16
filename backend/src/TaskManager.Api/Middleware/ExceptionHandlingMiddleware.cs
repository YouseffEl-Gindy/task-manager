using TaskManager.Api.Dtos.Common;

namespace TaskManager.Api.Middleware;

/// <summary>Turns unhandled exceptions into <c>500 internal_error</c> with no exception details.</summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client went away; there is no one to answer.
        }
        catch (BadHttpRequestException ex)
        {
            _logger.LogInformation(ex, "Bad HTTP request");
            await TryWriteAsync(context, ex.StatusCode, ApiErrorResponses.CodeForStatus(ex.StatusCode));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            await TryWriteAsync(context, StatusCodes.Status500InternalServerError, ApiErrorCodes.InternalError);
        }
    }

    private static async Task TryWriteAsync(HttpContext context, int status, string code)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        await ApiErrorResponses.WriteAsync(context, status, code);
    }
}
