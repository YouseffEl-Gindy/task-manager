namespace TaskManager.Api.Data;

public enum DatabaseProvider
{
    Sqlite,
    SqlServer,
}

public enum DatabaseSchemaMode
{
    None,
    Migrate,
    EnsureCreated,
}

/// <summary>Validated database configuration (<c>Database:Provider</c>, <c>Database:Schema</c>, <c>ConnectionStrings:Default</c>).</summary>
public sealed record DatabaseSettings(DatabaseProvider Provider, DatabaseSchemaMode Schema, string ConnectionString)
{
    public const string ProviderKey = "Database:Provider";
    public const string SchemaKey = "Database:Schema";
    public const string ConnectionStringName = "Default";

    /// <summary>Reads and validates the settings. Throws <see cref="InvalidOperationException"/> so startup fails (AC-DB-1).</summary>
    public static DatabaseSettings FromConfiguration(IConfiguration configuration)
    {
        var provider = ParseProvider(configuration[ProviderKey]);
        var schema = ParseSchema(configuration[SchemaKey]);
        EnsureCompatible(provider, schema);

        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:{ConnectionStringName} is missing. Set it with an environment variable (ConnectionStrings__Default) or user-secrets.");
        }

        return new DatabaseSettings(provider, schema, connectionString);
    }

    public static DatabaseProvider ParseProvider(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{ProviderKey} is missing. Use 'Sqlite' or 'SqlServer'.");
        }

        return value.Trim() switch
        {
            var v when v.Equals(nameof(DatabaseProvider.Sqlite), StringComparison.OrdinalIgnoreCase) => DatabaseProvider.Sqlite,
            var v when v.Equals(nameof(DatabaseProvider.SqlServer), StringComparison.OrdinalIgnoreCase) => DatabaseProvider.SqlServer,
            _ => throw new InvalidOperationException($"{ProviderKey} '{value}' is not supported. Use 'Sqlite' or 'SqlServer'."),
        };
    }

    public static DatabaseSchemaMode ParseSchema(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DatabaseSchemaMode.None;
        }

        return value.Trim() switch
        {
            var v when v.Equals(nameof(DatabaseSchemaMode.None), StringComparison.OrdinalIgnoreCase) => DatabaseSchemaMode.None,
            var v when v.Equals(nameof(DatabaseSchemaMode.Migrate), StringComparison.OrdinalIgnoreCase) => DatabaseSchemaMode.Migrate,
            var v when v.Equals(nameof(DatabaseSchemaMode.EnsureCreated), StringComparison.OrdinalIgnoreCase) => DatabaseSchemaMode.EnsureCreated,
            _ => throw new InvalidOperationException($"{SchemaKey} '{value}' is not supported. Use 'None', 'Migrate' or 'EnsureCreated'."),
        };
    }

    public static void EnsureCompatible(DatabaseProvider provider, DatabaseSchemaMode schema)
    {
        if (schema == DatabaseSchemaMode.Migrate && provider == DatabaseProvider.Sqlite)
        {
            throw new InvalidOperationException(
                "Database:Schema=Migrate cannot be used with Database:Provider=Sqlite. The migrations target SQL Server only; use EnsureCreated.");
        }

        if (schema == DatabaseSchemaMode.EnsureCreated && provider == DatabaseProvider.SqlServer)
        {
            throw new InvalidOperationException(
                "Database:Schema=EnsureCreated cannot be used with Database:Provider=SqlServer. Use Migrate.");
        }
    }
}
