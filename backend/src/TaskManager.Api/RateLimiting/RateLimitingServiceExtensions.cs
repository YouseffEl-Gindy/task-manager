using System.Globalization;
using System.Threading.RateLimiting;
using TaskManager.Api.Dtos.Common;
using TaskManager.Api.Middleware;

namespace TaskManager.Api.RateLimiting;

public static class RateLimitingServiceExtensions
{
    /// <summary>Policy for register, login, change-password and delete account. One fixed window per client IP, shared by those endpoints.</summary>
    public const string AuthPolicy = "auth";

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = RateLimitingSettings.FromConfiguration(configuration);
        services.AddSingleton(settings);
        var window = TimeSpan.FromSeconds(settings.AuthWindowSeconds);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(AuthPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ClientIpResolver.Resolve(context, settings.UseForwardedFor),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.AuthPermitLimit,
                        Window = window,
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }));

            options.OnRejected = async (context, _) =>
            {
                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var value) ? value : window;
                var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
                context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
                await ApiErrorResponses.WriteAsync(
                    context.HttpContext,
                    StatusCodes.Status429TooManyRequests,
                    ApiErrorCodes.RateLimited);
            };
        });

        return services;
    }
}
