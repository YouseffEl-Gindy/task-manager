using Microsoft.AspNetCore.Mvc;
using TaskManager.Api.Services;
using TaskManager.Api.Validation;

namespace TaskManager.Api.Controllers;

/// <summary>Maps a failed <see cref="ServiceOutcome"/> to its <c>ApiError</c> response.</summary>
public static class ServiceResultExtensions
{
    public static IActionResult ToErrorResult(this ControllerBase controller, ServiceOutcome outcome, ValidationErrors? errors)
    {
        var context = controller.HttpContext;
        return outcome switch
        {
            ServiceOutcome.NotFound => ApiErrorResults.NotFound(context),
            ServiceOutcome.ValidationFailed => ApiErrorResults.Validation(context, errors ?? new ValidationErrors()),
            ServiceOutcome.Unauthorized => ApiErrorResults.Unauthorized(context),
            ServiceOutcome.InvalidCredentials => ApiErrorResults.InvalidCredentials(context),
            ServiceOutcome.EmailTaken => ApiErrorResults.EmailTaken(context),
            _ => throw new InvalidOperationException($"Outcome '{outcome}' is not an error."),
        };
    }
}
