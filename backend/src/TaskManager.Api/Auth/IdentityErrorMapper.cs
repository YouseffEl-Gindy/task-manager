using Microsoft.AspNetCore.Identity;
using TaskManager.Api.Validation;

namespace TaskManager.Api.Auth;

/// <summary>Result of mapping Identity errors: either <c>email_taken</c>, or field-keyed validation errors.</summary>
public sealed record IdentityErrorMapping(bool EmailTaken, ValidationErrors Errors);

/// <summary>Maps <see cref="IdentityError"/> codes to API error keys (SPEC FR-A3).</summary>
public static class IdentityErrorMapper
{
    public const string CurrentPasswordIncorrectMessage = "Current password is incorrect.";

    private static readonly HashSet<string> PasswordCodes = new(StringComparer.Ordinal)
    {
        nameof(IdentityErrorDescriber.PasswordTooShort),
        nameof(IdentityErrorDescriber.PasswordRequiresDigit),
        nameof(IdentityErrorDescriber.PasswordRequiresLower),
        nameof(IdentityErrorDescriber.PasswordRequiresUpper),
        nameof(IdentityErrorDescriber.PasswordRequiresNonAlphanumeric),
        nameof(IdentityErrorDescriber.PasswordRequiresUniqueChars),
    };

    /// <param name="errors">Identity errors.</param>
    /// <param name="passwordField">Key for password rule errors: <c>password</c> (register) or <c>newPassword</c> (change password).</param>
    public static IdentityErrorMapping Map(IEnumerable<IdentityError> errors, string passwordField)
    {
        var result = new ValidationErrors();
        var emailTaken = false;

        foreach (var error in errors)
        {
            switch (error.Code)
            {
                case nameof(IdentityErrorDescriber.DuplicateUserName):
                case nameof(IdentityErrorDescriber.DuplicateEmail):
                    emailTaken = true;
                    break;
                case nameof(IdentityErrorDescriber.InvalidEmail):
                case nameof(IdentityErrorDescriber.InvalidUserName):
                    result.Add("email", "Email is not a valid email address.");
                    break;
                case nameof(IdentityErrorDescriber.PasswordMismatch):
                    result.Add("currentPassword", CurrentPasswordIncorrectMessage);
                    break;
                case var code when PasswordCodes.Contains(code):
                    result.Add(passwordField, error.Description);
                    break;
                default:
                    result.Add(ModelStateErrorMapper.BodyKey, "The request could not be processed.");
                    break;
            }
        }

        return new IdentityErrorMapping(emailTaken, result);
    }
}
