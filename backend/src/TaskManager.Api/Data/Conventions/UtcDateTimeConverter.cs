using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TaskManager.Api.Data.Conventions;

/// <summary>
/// Model-wide converter: values written are normalized to UTC, values read are marked <see cref="DateTimeKind.Utc"/>.
/// EF applies it to nullable <see cref="DateTime"/> properties as well.
/// </summary>
public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(v => ToStore(v), v => FromStore(v))
    {
    }

    public static DateTime ToStore(DateTime value) =>
        value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    public static DateTime FromStore(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
