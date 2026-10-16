using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace TaskManager.Api.Data;

/// <summary>Case-insensitive column collations per provider (SPEC 6.0).</summary>
public static class ProviderCollations
{
    public const string Sqlite = "NOCASE";
    public const string SqlServer = "Latin1_General_100_CI_AS";

    public static string CaseInsensitiveFor(DatabaseFacade database)
    {
        if (database.IsSqlServer())
        {
            return SqlServer;
        }

        if (database.IsSqlite())
        {
            return Sqlite;
        }

        throw new InvalidOperationException($"Unsupported database provider '{database.ProviderName}'.");
    }
}
