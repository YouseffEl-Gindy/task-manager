using System.Text.RegularExpressions;
using TaskManager.Api.Data.Configurations;

namespace TaskManager.Api.Validation;

/// <summary>Field rules shared by several requests (API.md section 5). Strings are trimmed before checks, passwords never.</summary>
public static partial class FieldRules
{
    public const int EmailMaxLength = 254;
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;
    public const int SearchMaxLength = 100;

    public static string? Trim(string? value) => value?.Trim();

    /// <summary>Trimmed and lowercased, as stored and looked up.</summary>
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    /// <summary>Trimmed; blank becomes <c>null</c>.</summary>
    public static string? NormalizeOptionalText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    public static void DisplayName(ValidationErrors errors, string? value, string field = "displayName")
    {
        var trimmed = Trim(value);
        if (string.IsNullOrEmpty(trimmed))
        {
            errors.Add(field, "Display name is required.");
        }
        else if (trimmed.Length > UserConfiguration.DisplayNameMaxLength)
        {
            errors.Add(field, $"Display name must be at most {UserConfiguration.DisplayNameMaxLength} characters.");
        }
    }

    public static void Email(ValidationErrors errors, string? value, bool checkFormat, string field = "email")
    {
        var trimmed = Trim(value);
        if (string.IsNullOrEmpty(trimmed))
        {
            errors.Add(field, "Email is required.");
        }
        else if (trimmed.Length > EmailMaxLength)
        {
            errors.Add(field, $"Email must be at most {EmailMaxLength} characters.");
        }
        else if (checkFormat && !EmailFormat().IsMatch(trimmed))
        {
            errors.Add(field, "Email is not a valid email address.");
        }
    }

    /// <summary>Full password rules: 8-128 chars, at least one ASCII letter and one digit. Not trimmed.</summary>
    public static void NewPassword(ValidationErrors errors, string? value, string field)
    {
        if (string.IsNullOrEmpty(value))
        {
            errors.Add(field, "Password is required.");
            return;
        }

        if (value.Length < PasswordMinLength)
        {
            errors.Add(field, $"Password must be at least {PasswordMinLength} characters.");
        }

        if (value.Length > PasswordMaxLength)
        {
            errors.Add(field, $"Password must be at most {PasswordMaxLength} characters.");
        }

        if (!value.Any(char.IsAsciiLetter))
        {
            errors.Add(field, "Password must contain at least one letter.");
        }

        if (!value.Any(char.IsAsciiDigit))
        {
            errors.Add(field, "Password must contain at least one digit.");
        }
    }

    /// <summary>An existing password (login, current password, delete account): required, not empty, max 128. No complexity check.</summary>
    public static void ExistingPassword(ValidationErrors errors, string? value, string field)
    {
        if (string.IsNullOrEmpty(value))
        {
            errors.Add(field, "Password is required.");
        }
        else if (value.Length > PasswordMaxLength)
        {
            errors.Add(field, $"Password must be at most {PasswordMaxLength} characters.");
        }
    }

    public static void Title(ValidationErrors errors, string? value, string field = "title")
    {
        var trimmed = Trim(value);
        if (string.IsNullOrEmpty(trimmed))
        {
            errors.Add(field, "Title is required.");
        }
        else if (trimmed.Length > TaskItemConfiguration.TitleMaxLength)
        {
            errors.Add(field, $"Title must be at most {TaskItemConfiguration.TitleMaxLength} characters.");
        }
    }

    public static void Description(ValidationErrors errors, string? value, string field = "description")
    {
        var trimmed = Trim(value);
        if (trimmed is not null && trimmed.Length > TaskItemConfiguration.DescriptionMaxLength)
        {
            errors.Add(field, $"Description must be at most {TaskItemConfiguration.DescriptionMaxLength} characters.");
        }
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailFormat();
}
