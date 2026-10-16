using System.Security.Claims;

namespace TaskManager.Api.Auth;

public static class CurrentUserExtensions
{
    /// <summary>The signed-in user's id from the session (Identity's NameIdentifier claim). The client never sends it.</summary>
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(value, out var id))
        {
            return id;
        }

        throw new InvalidOperationException("The current principal has no valid user id claim.");
    }
}
