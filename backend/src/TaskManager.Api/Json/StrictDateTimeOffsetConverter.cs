using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace TaskManager.Api.Json;

/// <summary>
/// Reads ISO 8601 date-times that carry an explicit offset or <c>Z</c>
/// (for example <c>2026-10-12T17:00:00+02:00</c>). Values with no offset, or a date only, are rejected.
/// </summary>
public sealed partial class StrictDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public const string InvalidFormatMessage =
        "The value must be an ISO 8601 date and time with an offset or Z, for example 2026-10-12T15:00:00Z.";

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException(InvalidFormatMessage);
        }

        var text = reader.GetString();
        if (TryParse(text, out var value))
        {
            return value;
        }

        throw new JsonException(InvalidFormatMessage);
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString("O", CultureInfo.InvariantCulture));

    public static bool TryParse(string? text, out DateTimeOffset value)
    {
        value = default;
        if (string.IsNullOrEmpty(text) || !IsoWithOffset().IsMatch(text))
        {
            return false;
        }

        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    }

    // yyyy-MM-ddTHH:mm[:ss[.fffffff]] followed by Z or +HH:mm / -HH:mm.
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d{1,7})?)?(Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex IsoWithOffset();
}
