namespace TaskManager.Api.Dtos.Common;

public static class ApiErrorCodes
{
    public const string ValidationFailed = "validation_failed";
    public const string BadRequest = "bad_request";
    public const string InvalidCredentials = "invalid_credentials";
    public const string Unauthorized = "unauthorized";
    public const string CsrfFailed = "csrf_failed";
    public const string NotFound = "not_found";
    public const string EmailTaken = "email_taken";
    public const string RateLimited = "rate_limited";
    public const string InternalError = "internal_error";
}
