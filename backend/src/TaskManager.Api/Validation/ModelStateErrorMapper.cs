using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TaskManager.Api.Json;

namespace TaskManager.Api.Validation;

/// <summary>
/// Converts MVC model state (JSON deserialization and query binding errors) into camelCase field keys.
/// JSON errors that can't be tied to a field use the key <c>body</c>. Raw serializer messages are never returned.
/// </summary>
public static partial class ModelStateErrorMapper
{
    public const string BodyKey = "body";
    public const string RequiredMessage = "This field is required.";
    public const string InvalidValueMessage = "The value is not valid.";
    public const string InvalidJsonMessage = "The request body is not valid JSON.";

    public static ValidationErrors Map(ModelStateDictionary modelState, IReadOnlyCollection<string> bodyParameterNames)
    {
        var errors = new ValidationErrors();
        foreach (var (key, entry) in modelState)
        {
            foreach (var error in entry.Errors)
            {
                MapError(errors, key, error.ErrorMessage, bodyParameterNames);
            }
        }

        if (errors.IsValid)
        {
            errors.Add(BodyKey, InvalidJsonMessage);
        }

        return errors;
    }

    public static void MapError(ValidationErrors errors, string key, string? message, IReadOnlyCollection<string> bodyParameterNames)
    {
        var missing = MissingRequiredProperties(message);
        if (missing.Count > 0)
        {
            foreach (var name in missing)
            {
                errors.Add(name, RequiredMessage);
            }

            return;
        }

        var isBodyError = key.Length == 0 || key.StartsWith('$') || bodyParameterNames.Contains(key, StringComparer.OrdinalIgnoreCase);
        var field = isBodyError ? FieldFromJsonPath(key) : FieldFromModelKey(key);

        string text;
        if (!isBodyError)
        {
            text = string.IsNullOrEmpty(message) ? InvalidValueMessage : message;
        }
        else if (message is not null && message.StartsWith(StrictDateTimeOffsetConverter.InvalidFormatMessage, StringComparison.Ordinal))
        {
            text = StrictDateTimeOffsetConverter.InvalidFormatMessage;
        }
        else
        {
            text = field == BodyKey ? InvalidJsonMessage : InvalidValueMessage;
        }

        errors.Add(field, text);
    }

    /// <summary><c>$.dueDate</c> -> <c>dueDate</c>; <c>$</c>, empty, or anything unparseable -> <c>body</c>.</summary>
    public static string FieldFromJsonPath(string key)
    {
        if (!key.StartsWith("$.", StringComparison.Ordinal))
        {
            return BodyKey;
        }

        var rest = key[2..];
        var end = rest.IndexOfAny(['.', '[']);
        var first = end < 0 ? rest : rest[..end];
        return first.Length == 0 ? BodyKey : JsonNamingPolicy.CamelCase.ConvertName(first);
    }

    /// <summary>MVC binding keys (<c>Status</c>, <c>query.SortBy</c>) -> camelCase last segment.</summary>
    public static string FieldFromModelKey(string key)
    {
        var last = key[(key.LastIndexOf('.') + 1)..];
        return last.Length == 0 ? BodyKey : JsonNamingPolicy.CamelCase.ConvertName(last);
    }

    /// <summary>
    /// Parses System.Text.Json's "missing required properties including: 'a', 'b'." message
    /// (a <c>[JsonRequired]</c> key that is absent). The names are JSON (camelCase) names.
    /// </summary>
    public static IReadOnlyList<string> MissingRequiredProperties(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return [];
        }

        var match = MissingRequired().Match(message);
        if (!match.Success)
        {
            return [];
        }

        return QuotedName().Matches(match.Groups["names"].Value).Select(m => m.Groups[1].Value).ToList();
    }

    [GeneratedRegex(@"missing required properties including: (?<names>.*)$", RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex MissingRequired();

    [GeneratedRegex(@"'([^']+)'", RegexOptions.CultureInvariant)]
    private static partial Regex QuotedName();
}
