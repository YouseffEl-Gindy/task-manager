using TaskManager.Api.Validation;

namespace TaskManager.Api.Services;

public enum ServiceOutcome
{
    Success,
    NotFound,
    ValidationFailed,
    Unauthorized,
    InvalidCredentials,
    EmailTaken,
}

/// <summary>Outcome of a service call; controllers map it to a status code and an <c>ApiError</c>.</summary>
public sealed record ServiceResult<T>(ServiceOutcome Outcome, T? Value = default, ValidationErrors? Errors = null)
{
    public static ServiceResult<T> Ok(T value) => new(ServiceOutcome.Success, value);

    public static ServiceResult<T> Fail(ServiceOutcome outcome) => new(outcome);

    public static ServiceResult<T> Invalid(ValidationErrors errors) => new(ServiceOutcome.ValidationFailed, Errors: errors);
}

/// <summary>Outcome of a service call with no value.</summary>
public sealed record ServiceResult(ServiceOutcome Outcome, ValidationErrors? Errors = null)
{
    public static ServiceResult Ok() => new(ServiceOutcome.Success);

    public static ServiceResult Fail(ServiceOutcome outcome) => new(outcome);

    public static ServiceResult Invalid(ValidationErrors errors) => new(ServiceOutcome.ValidationFailed, errors);
}
