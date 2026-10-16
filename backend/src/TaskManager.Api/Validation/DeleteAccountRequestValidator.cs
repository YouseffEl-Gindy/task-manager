using TaskManager.Api.Dtos.Account;

namespace TaskManager.Api.Validation;

public sealed class DeleteAccountRequestValidator : IRequestValidator<DeleteAccountRequest>
{
    public ValidationErrors Validate(DeleteAccountRequest request)
    {
        var errors = new ValidationErrors();
        FieldRules.ExistingPassword(errors, request.Password, "password");
        return errors;
    }
}
