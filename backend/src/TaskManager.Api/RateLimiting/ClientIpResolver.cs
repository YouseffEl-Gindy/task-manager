namespace TaskManager.Api.RateLimiting;

/// <summary>
/// The rate-limit partition key. Behind the Vercel proxy the browser's IP is the left-most <c>X-Forwarded-For</c>
/// value (App Service appends its own hop to the right). Otherwise the connection IP is used.
/// </summary>
public static class ClientIpResolver
{
    public const string ForwardedForHeader = "X-Forwarded-For";
    public const string Unknown = "unknown";

    public static string Resolve(HttpContext context, bool useForwardedFor)
    {
        if (useForwardedFor)
        {
            var forwarded = LeftMostForwardedFor(context.Request.Headers[ForwardedForHeader]);
            if (forwarded is not null)
            {
                return forwarded;
            }
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? Unknown;
    }

    public static string? LeftMostForwardedFor(IEnumerable<string?> headerValues)
    {
        foreach (var headerValue in headerValues)
        {
            if (string.IsNullOrWhiteSpace(headerValue))
            {
                continue;
            }

            var first = headerValue.Split(',')[0].Trim();
            return first.Length == 0 ? null : first;
        }

        return null;
    }
}
