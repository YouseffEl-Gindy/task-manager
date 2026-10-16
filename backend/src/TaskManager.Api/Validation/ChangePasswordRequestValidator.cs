using TaskManager.Api.Dtos.Account;

namespace TaskManager.Api.Validation;

public sealed class ChangePasswordRequestValidator : IRequestValidator<ChangePasswordRequest>
{
    public const string MustDifferMessage = "New password must be different from the current password.";

    public ValidationErrors Validate(ChangePasswordRequest request)
    {
        var errors = new ValidationErrors();
        FieldRules.ExistingPassword(errors, request.CurrentPassword, "currentPassword");
        FieldRules.NewPassword(errors, request.NewPassword, "newPassword");

        if (!string.IsNullOrEmpty(request.NewPassword)
            && string.Equals(request.NewPassword, request.CurrentPassword, StringComparison.Ordinal))
        {
            errors.Add("newPassword", MustDifferMessage);
        }

        return errors;
    }
}
