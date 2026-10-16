using Microsoft.Extensions.Configuration;
using TaskManager.Api.Data;

namespace TaskManager.Api.UnitTests.Data;

/// <summary>Provider switch and schema guards (SPEC 5.5, AC-DB-1).</summary>
public sealed class DatabaseSettingsTests
{
    private static IConfiguration Config(string? provider, string? schema, string? connection = "Data Source=x.db") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = provider,
                ["Database:Schema"] = schema,
                ["ConnectionStrings:Default"] = connection,
            })
            .Build();

    [Theory]
    [InlineData("Sqlite", "EnsureCreated", DatabaseProvider.Sqlite, DatabaseSchemaMode.EnsureCreated)]
    [InlineData("sqlite", null, DatabaseProvider.Sqlite, DatabaseSchemaMode.None)]
    [InlineData("SqlServer", "Migrate", DatabaseProvider.SqlServer, DatabaseSchemaMode.Migrate)]
    [InlineData("SqlServer", "None", DatabaseProvider.SqlServer, DatabaseSchemaMode.None)]
    public void FromConfiguration_AcceptsValidCombinations(string provider, string? schema, DatabaseProvider expectedProvider, DatabaseSchemaMode expectedSchema)
    {
        var settings = DatabaseSettings.FromConfiguration(Config(provider, schema));

        Assert.Equal(expectedProvider, settings.Provider);
        Assert.Equal(expectedSchema, settings.Schema);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Postgres")]
    [InlineData("1")]
    public void FromConfiguration_MissingOrUnknownProvider_Throws(string? provider)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => DatabaseSettings.FromConfiguration(Config(provider, "None")));
        Assert.Contains("Database:Provider", ex.Message);
    }

    [Theory]
    [InlineData("Sqlite", "Migrate")]
    [InlineData("SqlServer", "EnsureCreated")]
    public void FromConfiguration_SchemaThatDoesNotFitProvider_Throws(string provider, string schema)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => DatabaseSettings.FromConfiguration(Config(provider, schema)));
        Assert.Contains("Database:Schema", ex.Message);
    }

    [Fact]
    public void FromConfiguration_UnknownSchema_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => DatabaseSettings.FromConfiguration(Config("Sqlite", "Recreate")));
    }

    [Fact]
    public void FromConfiguration_MissingConnectionString_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => DatabaseSettings.FromConfiguration(Config("Sqlite", "None", connection: null)));
        Assert.Contains("ConnectionStrings:Default", ex.Message);
    }
}
