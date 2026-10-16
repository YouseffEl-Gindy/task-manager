using TaskManager.Api.Dtos.Auth;

namespace TaskManager.Api.Validation;

public sealed class RegisterRequestValidator : IRequestValidator<RegisterRequest>
{
    public ValidationErrors Validate(RegisterRequest request)
    {
        var errors = new ValidationErrors();
        FieldRules.DisplayName(errors, request.DisplayName);
        FieldRules.Email(errors, request.Email, checkFormat: true);
        FieldRules.NewPassword(errors, request.Password, "password");
        return errors;
    }
}
