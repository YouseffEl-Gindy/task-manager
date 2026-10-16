using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaskManager.Api.Auth;
using TaskManager.Api.Dtos.Account;
using TaskManager.Api.Dtos.Auth;
using TaskManager.Api.Dtos.Common;
using TaskManager.Api.RateLimiting;
using TaskManager.Api.Services;

namespace TaskManager.Api.Controllers;

/// <summary>API.md 5.5 to 5.7.</summary>
[ApiController]
[Authorize]
[Route("api/v1/account")]
[Produces("application/json")]
[ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
public sealed class AccountController : ControllerBase
{
    private readonly IAccountService _accounts;

    public AccountController(IAccountService accounts)
    {
        _accounts = accounts;
    }

    [HttpPut("profile")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var result = await _accounts.UpdateProfileAsync(User.GetUserId(), request, cancellationToken);
        return result.Outcome == ServiceOutcome.Success
            ? Ok(result.Value)
            : this.ToErrorResult(result.Outcome, result.Errors);
    }

    [HttpPost("change-password")]
    [EnableRateLimiting(RateLimitingServiceExtensions.AuthPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await _accounts.ChangePasswordAsync(User.GetUserId(), request, cancellationToken);
        return result.Outcome == ServiceOutcome.Success
            ? NoContent()
            : this.ToErrorResult(result.Outcome, result.Errors);
    }

    [HttpPost("delete")]
    [EnableRateLimiting(RateLimitingServiceExtensions.AuthPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Delete([FromBody] DeleteAccountRequest request, CancellationToken cancellationToken)
    {
        var result = await _accounts.DeleteAccountAsync(User.GetUserId(), request, cancellationToken);
        return result.Outcome == ServiceOutcome.Success
            ? NoContent()
            : this.ToErrorResult(result.Outcome, result.Errors);
    }
}
