using TaskManager.Api.Dtos.Auth;

namespace TaskManager.Api.Validation;

public sealed class LoginRequestValidator : IRequestValidator<LoginRequest>
{
    public ValidationErrors Validate(LoginRequest request)
    {
        var errors = new ValidationErrors();
        FieldRules.Email(errors, request.Email, checkFormat: false);
        FieldRules.ExistingPassword(errors, request.Password, "password");
        return errors;
    }
}
