using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TaskManager.Api.IntegrationTests;

/// <summary>AC-DB-1: the app refuses to start with a missing/unknown provider or a schema mode that doesn't fit it.</summary>
public sealed class StartupGuardTests
{
    private sealed class MisconfiguredFactory : WebApplicationFactory<Program>
    {
        private readonly string? _provider;
        private readonly string? _schema;

        public MisconfiguredFactory(string? provider, string? schema)
        {
            _provider = provider;
            _schema = schema;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Database:Provider", _provider);
            builder.UseSetting("Database:Schema", _schema);
            builder.UseSetting("ConnectionStrings:Default", "Data Source=never-created.db");
        }
    }

    [Theory]
    [InlineData(null, "None", "Database:Provider")]
    [InlineData("Postgres", "None", "Database:Provider")]
    [InlineData("Sqlite", "Migrate", "Database:Schema")]
    [InlineData("SqlServer", "EnsureCreated", "Database:Schema")]
    public void AC_DB_1_InvalidDatabaseConfiguration_FailsAtStartup(string? provider, string schema, string expectedKey)
    {
        using var factory = new MisconfiguredFactory(provider, schema);

        var ex = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(ex);
        var messages = string.Join(" | ", Flatten(ex));
        Assert.Contains(expectedKey, messages);
    }

    private static IEnumerable<string> Flatten(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            yield return current.Message;
            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    yield return inner.Message;
                }
            }
        }
    }
}
