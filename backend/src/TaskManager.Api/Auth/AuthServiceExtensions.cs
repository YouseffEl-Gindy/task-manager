using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using TaskManager.Api.Data;
using TaskManager.Api.Entities;

namespace TaskManager.Api.Auth;

public static class AuthServiceExtensions
{
    public const string SessionCookieName = "tm_session";
    public const string DataProtectionApplicationName = "TaskManager";

    /// <summary>Identity (no roles), the <c>tm_session</c> cookie, security stamp validation and Data Protection keys (SPEC 4.1).</summary>
    public static IServiceCollection AddAppIdentity(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = AuthSettings.FromConfiguration(configuration);
        services.AddSingleton(settings);
        services.AddHttpContextAccessor();

        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();

        services.AddIdentityCore<User>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                options.SignIn.RequireConfirmedEmail = false;
                options.SignIn.RequireConfirmedAccount = false;

                options.User.RequireUniqueEmail = true;
                // UserName = email; email format is enforced by DTO validation, so allow any characters here.
                options.User.AllowedUserNameCharacters = string.Empty;
            })
            .AddUserManager<AppUserManager>()
            .AddSignInManager()
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddScoped<CookieAuthEvents>();
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = SessionCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.Path = "/";
            options.Cookie.Domain = null;
            options.Cookie.MaxAge = null;
            options.Cookie.SecurePolicy = settings.CookieSecure;
            options.ExpireTimeSpan = TimeSpan.FromHours(settings.SessionHours);
            options.SlidingExpiration = true;
            options.EventsType = typeof(CookieAuthEvents);
        });
        services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
            .Configure<TimeProvider>((options, timeProvider) => options.TimeProvider = timeProvider);

        services.AddOptions<SecurityStampValidatorOptions>()
            .Configure<TimeProvider>((options, timeProvider) =>
            {
                options.ValidationInterval = TimeSpan.FromSeconds(settings.SecurityStampValidationSeconds);
                options.TimeProvider = timeProvider;
            });

        services.AddDataProtection()
            .SetApplicationName(DataProtectionApplicationName)
            .PersistKeysToDbContext<AppDbContext>();

        services.AddAuthorization();
        return services;
    }
}
