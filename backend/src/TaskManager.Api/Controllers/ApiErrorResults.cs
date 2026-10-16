using Microsoft.AspNetCore.Mvc;
using TaskManager.Api.Dtos.Common;
using TaskManager.Api.Middleware;
using TaskManager.Api.Validation;

namespace TaskManager.Api.Controllers;

/// <summary>MVC results that carry an <see cref="ApiError"/> body.</summary>
public static class ApiErrorResults
{
    public static ObjectResult Error(HttpContext context, int status, string code, ValidationErrors? errors = null) =>
        new(ApiErrorResponses.Create(context, status, code, errors?.ToDictionary()))
        {
            StatusCode = status,
            ContentTypes = { "application/json" },
        };

    public static ObjectResult Validation(HttpContext context, ValidationErrors errors) =>
        Error(context, StatusCodes.Status400BadRequest, ApiErrorCodes.ValidationFailed, errors);

    public static ObjectResult NotFound(HttpContext context) =>
        Error(context, StatusCodes.Status404NotFound, ApiErrorCodes.NotFound);

    public static ObjectResult Unauthorized(HttpContext context) =>
        Error(context, StatusCodes.Status401Unauthorized, ApiErrorCodes.Unauthorized);

    public static ObjectResult InvalidCredentials(HttpContext context) =>
        Error(context, StatusCodes.Status401Unauthorized, ApiErrorCodes.InvalidCredentials);

    public static ObjectResult EmailTaken(HttpContext context) =>
        Error(context, StatusCodes.Status409Conflict, ApiErrorCodes.EmailTaken);
}
