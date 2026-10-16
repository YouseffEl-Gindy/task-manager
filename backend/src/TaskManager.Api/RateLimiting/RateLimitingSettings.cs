namespace TaskManager.Api.RateLimiting;

/// <summary>The <c>RateLimiting</c> configuration section (SPEC 5.3, 5.5).</summary>
public sealed class RateLimitingSettings
{
    public const string SectionName = "RateLimiting";

    public int AuthPermitLimit { get; set; } = 10;

    public int AuthWindowSeconds { get; set; } = 60;

    public bool UseForwardedFor { get; set; }

    public static RateLimitingSettings FromConfiguration(IConfiguration configuration)
    {
        var settings = configuration.GetSection(SectionName).Get<RateLimitingSettings>() ?? new RateLimitingSettings();
        if (settings.AuthPermitLimit <= 0)
        {
            throw new InvalidOperationException("RateLimiting:AuthPermitLimit must be greater than 0.");
        }

        if (settings.AuthWindowSeconds <= 0)
        {
            throw new InvalidOperationException("RateLimiting:AuthWindowSeconds must be greater than 0.");
        }

        return settings;
    }
}
