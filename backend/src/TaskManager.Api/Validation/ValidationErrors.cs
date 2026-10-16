namespace TaskManager.Api.Validation;

/// <summary>Collects validation messages keyed by camelCase field name.</summary>
public sealed class ValidationErrors
{
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.Ordinal);

    public bool IsValid => _errors.Count == 0;

    public ValidationErrors Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out var messages))
        {
            messages = new List<string>();
            _errors[field] = messages;
        }

        if (!messages.Contains(message))
        {
            messages.Add(message);
        }

        return this;
    }

    public bool Has(string field) => _errors.ContainsKey(field);

    public IReadOnlyDictionary<string, string[]> ToDictionary() =>
        _errors.ToDictionary(e => e.Key, e => e.Value.ToArray(), StringComparer.Ordinal);

    public static ValidationErrors Single(string field, string message) => new ValidationErrors().Add(field, message);
}
