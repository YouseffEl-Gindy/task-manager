using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Auth;
using TaskManager.Api.Dtos.Account;
using TaskManager.Api.Dtos.Auth;
using TaskManager.Api.Entities;
using TaskManager.Api.Mapping;
using TaskManager.Api.Validation;

namespace TaskManager.Api.Services;

public sealed class AccountService : IAccountService
{
    public const string PasswordIncorrectMessage = "Password is incorrect.";

    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly TimeProvider _timeProvider;

    public AccountService(UserManager<User> userManager, SignInManager<User> signInManager, TimeProvider timeProvider)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _timeProvider = timeProvider;
    }

    public async Task<ServiceResult<UserDto>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = FieldRules.NormalizeEmail(request.Email);
        var user = new User
        {
            UserName = email,
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
        };

        IdentityResult result;
        try
        {
            result = await _userManager.CreateAsync(user, request.Password);
        }
        catch (DbUpdateException)
        {
            // Lost a race with a concurrent registration of the same email (unique index on NormalizedUserName).
            if (await _userManager.FindByEmailAsync(email) is not null)
            {
                return ServiceResult<UserDto>.Fail(ServiceOutcome.EmailTaken);
            }

            throw;
        }

        if (!result.Succeeded)
        {
            var mapping = IdentityErrorMapper.Map(result.Errors, passwordField: "password");
            return mapping.EmailTaken
                ? ServiceResult<UserDto>.Fail(ServiceOutcome.EmailTaken)
                : ServiceResult<UserDto>.Invalid(mapping.Errors);
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        return ServiceResult<UserDto>.Ok(user.ToDto());
    }

    public async Task<ServiceResult<UserDto>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = FieldRules.NormalizeEmail(request.Email);
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return ServiceResult<UserDto>.Fail(ServiceOutcome.InvalidCredentials);
        }

        var result = await _signInManager.PasswordSignInAsync(user, request.Password, isPersistent: false, lockoutOnFailure: true);
        return result.Succeeded
            ? ServiceResult<UserDto>.Ok(user.ToDto())
            : ServiceResult<UserDto>.Fail(ServiceOutcome.InvalidCredentials);
    }

    public Task LogoutAsync() => _signInManager.Context.SignOutAsync(IdentityConstants.ApplicationScheme);

    public async Task<ServiceResult<UserDto>> GetCurrentAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(userId);
        return user is null
            ? ServiceResult<UserDto>.Fail(ServiceOutcome.Unauthorized)
            : ServiceResult<UserDto>.Ok(user.ToDto());
    }

    public async Task<ServiceResult<UserDto>> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(userId);
        if (user is null)
        {
            return ServiceResult<UserDto>.Fail(ServiceOutcome.Unauthorized);
        }

        user.DisplayName = request.DisplayName.Trim();
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return ServiceResult<UserDto>.Invalid(IdentityErrorMapper.Map(result.Errors, "password").Errors);
        }

        return ServiceResult<UserDto>.Ok(user.ToDto());
    }

    public async Task<ServiceResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(userId);
        if (user is null)
        {
            return ServiceResult.Fail(ServiceOutcome.Unauthorized);
        }

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return ServiceResult.Invalid(IdentityErrorMapper.Map(result.Errors, passwordField: "newPassword").Errors);
        }

        // The security stamp changed: keep this session valid, other sessions end at their next stamp check.
        await _signInManager.RefreshSignInAsync(user);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteAccountAsync(Guid userId, DeleteAccountRequest request, CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(userId);
        if (user is null)
        {
            return ServiceResult.Fail(ServiceOutcome.Unauthorized);
        }

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return ServiceResult.Invalid(ValidationErrors.Single("password", PasswordIncorrectMessage));
        }

        // Tasks and Identity child rows are removed by the database cascade.
        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Deleting the user failed: " + string.Join(", ", result.Errors.Select(e => e.Code)));
        }

        await LogoutAsync();
        return ServiceResult.Ok();
    }

    private Task<User?> FindUserAsync(Guid userId) => _userManager.FindByIdAsync(userId.ToString());
}
