using TaskManager.Api.Dtos.Account;

namespace TaskManager.Api.Validation;

public sealed class UpdateProfileRequestValidator : IRequestValidator<UpdateProfileRequest>
{
    public ValidationErrors Validate(UpdateProfileRequest request)
    {
        var errors = new ValidationErrors();
        FieldRules.DisplayName(errors, request.DisplayName);
        return errors;
    }
}
