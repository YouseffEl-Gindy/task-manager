using Microsoft.AspNetCore.Http;

namespace TaskManager.Api.Auth;

/// <summary>The <c>Auth</c> configuration section (SPEC 5.5).</summary>
public sealed class AuthSettings
{
    public const string SectionName = "Auth";

    public double SessionHours { get; set; } = 8;

    public CookieSecurePolicy CookieSecure { get; set; } = CookieSecurePolicy.Always;

    public int SecurityStampValidationSeconds { get; set; } = 60;

    public static AuthSettings FromConfiguration(IConfiguration configuration)
    {
        var settings = configuration.GetSection(SectionName).Get<AuthSettings>() ?? new AuthSettings();
        settings.Validate();
        return settings;
    }

    public void Validate()
    {
        if (SessionHours <= 0)
        {
            throw new InvalidOperationException("Auth:SessionHours must be greater than 0.");
        }

        if (CookieSecure is not (CookieSecurePolicy.Always or CookieSecurePolicy.SameAsRequest))
        {
            throw new InvalidOperationException("Auth:CookieSecure must be 'Always' or 'SameAsRequest'.");
        }

        if (SecurityStampValidationSeconds < 0)
        {
            throw new InvalidOperationException("Auth:SecurityStampValidationSeconds must be 0 or more.");
        }
    }
}
