using TaskManager.Api.Dtos.Account;
using TaskManager.Api.Dtos.Auth;

namespace TaskManager.Api.Services;

/// <summary>Registration, sessions and account management on top of ASP.NET Core Identity.</summary>
public interface IAccountService
{
    /// <summary>Creates the user and signs them in. Outcomes: Success, ValidationFailed, EmailTaken.</summary>
    Task<ServiceResult<UserDto>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    /// <summary>Outcomes: Success, InvalidCredentials (unknown email, wrong password, or locked out).</summary>
    Task<ServiceResult<UserDto>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    /// <summary>Signs out and expires the session cookie. Safe without a session.</summary>
    Task LogoutAsync();

    /// <summary>Outcomes: Success, Unauthorized (the session's user no longer exists).</summary>
    Task<ServiceResult<UserDto>> GetCurrentAsync(Guid userId, CancellationToken cancellationToken);

    Task<ServiceResult<UserDto>> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken);

    /// <summary>Refreshes the current session on success. A wrong current password is ValidationFailed on <c>currentPassword</c>.</summary>
    Task<ServiceResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken);

    /// <summary>Deletes the user (tasks cascade) and signs out. A wrong password is ValidationFailed on <c>password</c>.</summary>
    Task<ServiceResult> DeleteAccountAsync(Guid userId, DeleteAccountRequest request, CancellationToken cancellationToken);
}
