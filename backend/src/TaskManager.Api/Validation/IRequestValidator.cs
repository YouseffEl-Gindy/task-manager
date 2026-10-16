namespace TaskManager.Api.Validation;

/// <summary>Validates one request DTO. Run by <see cref="RequestValidationFilter"/> before the action executes.</summary>
public interface IRequestValidator<in T>
{
    ValidationErrors Validate(T request);
}
