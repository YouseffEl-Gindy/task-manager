using System.Text;

namespace TaskManager.Api.Data;

/// <summary>
/// Builds a <c>%...%</c> LIKE pattern that matches the search text literally, using <c>\</c> as the escape character.
/// Both providers: escape <c>\</c>, <c>%</c>, <c>_</c>. SQL Server only: also escape <c>[</c> (SPEC FR-T7).
/// </summary>
public static class LikePattern
{
    public const char EscapeChar = '\\';
    public const string EscapeCharacter = "\\";

    public static string Contains(string text, bool isSqlServer) => $"%{Escape(text, isSqlServer)}%";

    public static string Escape(string text, bool isSqlServer)
    {
        ArgumentNullException.ThrowIfNull(text);
        var builder = new StringBuilder(text.Length + 8);
        foreach (var c in text)
        {
            if (c is EscapeChar or '%' or '_' || (isSqlServer && c == '['))
            {
                builder.Append(EscapeChar);
            }

            builder.Append(c);
        }

        return builder.ToString();
    }
}
