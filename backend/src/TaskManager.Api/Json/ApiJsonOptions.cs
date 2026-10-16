using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaskManager.Api.Json;

/// <summary>The one JSON configuration used by MVC and by middleware that writes <c>ApiError</c> bodies (API.md section 6).</summary>
public static class ApiJsonOptions
{
    public static JsonSerializerOptions Default { get; } = CreateDefault();

    public static void Apply(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.DictionaryKeyPolicy = null; // error keys are already camelCase field names
        options.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        options.Converters.Add(new StrictDateTimeOffsetConverter());
    }

    private static JsonSerializerOptions CreateDefault()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Apply(options);
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
