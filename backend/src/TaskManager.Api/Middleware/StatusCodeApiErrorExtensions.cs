namespace TaskManager.Api.Middleware;

public static class StatusCodeApiErrorExtensions
{
    /// <summary>
    /// Fills empty error responses under <c>/api</c> (routing 404/405, 415, ...) with an <c>ApiError</c> body.
    /// Responses that already have a body are left alone.
    /// </summary>
    public static IApplicationBuilder UseApiStatusCodeErrors(this IApplicationBuilder app) =>
        app.UseStatusCodePages(async statusContext =>
        {
            var context = statusContext.HttpContext;
            if (!ApiErrorResponses.IsApiPath(context))
            {
                return;
            }

            var status = context.Response.StatusCode;
            await ApiErrorResponses.WriteAsync(context, status, ApiErrorResponses.CodeForStatus(status));
        });
}
