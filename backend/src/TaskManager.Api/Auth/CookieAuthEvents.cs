using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using TaskManager.Api.Dtos.Common;
using TaskManager.Api.Middleware;

namespace TaskManager.Api.Auth;

/// <summary>
/// The session cookie never redirects: a challenge becomes <c>401 unauthorized</c> and a forbid becomes <c>403</c>,
/// both with an <c>ApiError</c> body (SPEC FR-A5). Also runs Identity's security stamp validation (FR-A9).
/// </summary>
public sealed class CookieAuthEvents : CookieAuthenticationEvents
{
    public override Task ValidatePrincipal(CookieValidatePrincipalContext context) =>
        SecurityStampValidator.ValidatePrincipalAsync(context);

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context) =>
        ApiErrorResponses.WriteAsync(context.HttpContext, StatusCodes.Status401Unauthorized, ApiErrorCodes.Unauthorized);

    // API.md defines no dedicated 403 code apart from csrf_failed. No endpoint uses role/policy authorization,
    // so this path is not reachable today; it uses the generic bad_request code so it never looks like a CSRF or session failure.
    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context) =>
        ApiErrorResponses.WriteAsync(
            context.HttpContext,
            StatusCodes.Status403Forbidden,
            ApiErrorCodes.BadRequest,
            message: "You do not have access to this resource.");

    public override Task RedirectToLogout(RedirectContext<CookieAuthenticationOptions> context) => Task.CompletedTask;

    public override Task RedirectToReturnUrl(RedirectContext<CookieAuthenticationOptions> context) => Task.CompletedTask;
}
