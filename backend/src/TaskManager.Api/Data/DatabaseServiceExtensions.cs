using Microsoft.EntityFrameworkCore;

namespace TaskManager.Api.Data;

public static class DatabaseServiceExtensions
{
    public const int SqlServerMaxRetryCount = 6;
    public static readonly TimeSpan SqlServerMaxRetryDelay = TimeSpan.FromSeconds(30);

    /// <summary>Registers <see cref="AppDbContext"/> for the configured provider. Fails if the configuration is missing or invalid.</summary>
    public static IServiceCollection AddAppDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = DatabaseSettings.FromConfiguration(configuration);
        services.AddSingleton(settings);

        services.AddDbContext<AppDbContext>(options =>
        {
            switch (settings.Provider)
            {
                case DatabaseProvider.Sqlite:
                    options.UseSqlite(settings.ConnectionString);
                    break;
                case DatabaseProvider.SqlServer:
                    options.UseSqlServer(
                        settings.ConnectionString,
                        sql => sql.EnableRetryOnFailure(SqlServerMaxRetryCount, SqlServerMaxRetryDelay, null));
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported database provider '{settings.Provider}'.");
            }
        });

        return services;
    }

    /// <summary>Runs the <c>Database:Schema</c> startup step (Migrate / EnsureCreated / None).</summary>
    public static async Task ApplyDatabaseSchemaAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var settings = services.GetRequiredService<DatabaseSettings>();
        if (settings.Schema == DatabaseSchemaMode.None)
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Guard against a provider swapped in by DI (for example in tests) that doesn't fit the schema mode.
        if (settings.Schema == DatabaseSchemaMode.Migrate)
        {
            if (!db.Database.IsSqlServer())
            {
                throw new InvalidOperationException("Database:Schema=Migrate requires the SqlServer provider.");
            }

            await db.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            if (!db.Database.IsSqlite())
            {
                throw new InvalidOperationException("Database:Schema=EnsureCreated requires the Sqlite provider.");
            }

            await db.Database.EnsureCreatedAsync(cancellationToken);
        }
    }
}
