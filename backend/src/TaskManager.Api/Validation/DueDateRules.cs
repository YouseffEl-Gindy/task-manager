namespace TaskManager.Api.Validation;

/// <summary>Due-date normalization and the "must be in the future" rule (SPEC FR-T9, FR-T10).</summary>
public static class DueDateRules
{
    public const string Field = "dueDate";
    public const string MustBeInFutureMessage = "Due date must be in the future.";

    /// <summary>Converts to a UTC <see cref="DateTime"/> truncated to whole seconds.</summary>
    public static DateTime? ToUtcWholeSeconds(DateTimeOffset? value)
    {
        if (value is null)
        {
            return null;
        }

        var ticks = value.Value.UtcTicks;
        return new DateTime(ticks - (ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc);
    }

    /// <summary>Create: a non-null due date must be strictly later than now.</summary>
    public static bool IsAllowedOnCreate(DateTime? newDueUtc, DateTime nowUtc) =>
        newDueUtc is null || newDueUtc.Value > nowUtc;

    /// <summary>
    /// Update: null is always allowed; the stored instant unchanged is always allowed (even if past);
    /// any other value (including when nothing was stored) must be strictly later than now.
    /// </summary>
    public static bool IsAllowedOnUpdate(DateTime? newDueUtc, DateTime? storedDueUtc, DateTime nowUtc)
    {
        if (newDueUtc is null)
        {
            return true;
        }

        if (storedDueUtc is not null && newDueUtc.Value == storedDueUtc.Value)
        {
            return true;
        }

        return newDueUtc.Value > nowUtc;
    }
}
