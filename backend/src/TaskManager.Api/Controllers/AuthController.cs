using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaskManager.Api.Auth;
using TaskManager.Api.Dtos.Auth;
using TaskManager.Api.Dtos.Common;
using TaskManager.Api.RateLimiting;
using TaskManager.Api.Services;

namespace TaskManager.Api.Controllers;

/// <summary>API.md 5.1 to 5.4.</summary>
[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly IAccountService _accounts;

    public AuthController(IAccountService accounts)
    {
        _accounts = accounts;
    }

    [HttpPost("register")]
    [EnableRateLimiting(RateLimitingServiceExtensions.AuthPolicy)]
    [ProducesResponseType<UserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiError>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await _accounts.RegisterAsync(request, cancellationToken);
        return result.Outcome == ServiceOutcome.Success
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : this.ToErrorResult(result.Outcome, result.Errors);
    }

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitingServiceExtensions.AuthPolicy)]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiError>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _accounts.LoginAsync(request, cancellationToken);
        return result.Outcome == ServiceOutcome.Success
            ? Ok(result.Value)
            : this.ToErrorResult(result.Outcome, result.Errors);
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        await _accounts.LogoutAsync();
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var result = await _accounts.GetCurrentAsync(User.GetUserId(), cancellationToken);
        if (result.Outcome == ServiceOutcome.Success)
        {
            return Ok(result.Value);
        }

        // The session's user was deleted: end the session too.
        await _accounts.LogoutAsync();
        return this.ToErrorResult(result.Outcome, result.Errors);
    }
}
