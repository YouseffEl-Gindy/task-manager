namespace TaskManager.Api.Dtos.Common;

/// <summary>Default <c>message</c> per error code (API.md section 3).</summary>
public static class ApiErrorMessages
{
    public static string For(string code) => code switch
    {
        ApiErrorCodes.ValidationFailed => "One or more fields are invalid.",
        ApiErrorCodes.InvalidCredentials => "Email or password is incorrect.",
        ApiErrorCodes.Unauthorized => "Authentication is required.",
        ApiErrorCodes.CsrfFailed => "Missing or invalid CSRF header.",
        ApiErrorCodes.NotFound => "The requested resource was not found.",
        ApiErrorCodes.EmailTaken => "An account with this email already exists.",
        ApiErrorCodes.RateLimited => "Too many requests. Please try again later.",
        ApiErrorCodes.InternalError => "An unexpected error occurred.",
        _ => "The request could not be processed.",
    };
}
