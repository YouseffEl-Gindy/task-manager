using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using TaskManager.Api.Auth;
using TaskManager.Api.Data;

namespace TaskManager.Api.IntegrationTests.Infrastructure;

/// <summary>
/// One factory per test class (SPEC 5.6).
/// <list type="bullet">
/// <item>Default: one SQLite in-memory connection kept open for the factory's lifetime; <c>AppDbContext</c> is re-registered
/// with <c>UseSqlite(thatConnection)</c> and <c>Database:Schema=EnsureCreated</c>.</item>
/// <item><c>TEST_DATABASE_PROVIDER=SqlServer</c> + <c>TEST_DATABASE_CONNECTION</c>: a uniquely named database per factory,
/// <c>Database:Schema=Migrate</c> (the real migrations), dropped with <c>EnsureDeleted()</c> on dispose.</item>
/// </list>
/// Tests in a class share the database, so each test creates its own users.
/// </summary>
public class TaskManagerApiFactory : WebApplicationFactory<Program>
{
    public const string ProviderVariable = "TEST_DATABASE_PROVIDER";
    public const string ConnectionVariable = "TEST_DATABASE_CONNECTION";
    public const string BaseAddress = "https://localhost"; // https so Secure cookies are sent back

    private readonly SqliteConnection? _sqliteConnection;
    private readonly string? _sqlServerConnectionString;
    private readonly Dictionary<string, string?> _settings;

    public TaskManagerApiFactory()
        : this(new Dictionary<string, string?>())
    {
    }

    protected TaskManagerApiFactory(IDictionary<string, string?> overrides)
    {
        IsSqlServer = string.Equals(Environment.GetEnvironmentVariable(ProviderVariable), "SqlServer", StringComparison.OrdinalIgnoreCase);

        _settings = new Dictionary<string, string?>
        {
            ["Auth:SecurityStampValidationSeconds"] = "0",
            ["Auth:CookieSecure"] = "Always",
            ["Auth:SessionHours"] = "8",
            // High by default so tests sharing the TestServer client IP don't trip it; rate-limit tests override it.
            ["RateLimiting:AuthPermitLimit"] = "100000",
            ["RateLimiting:AuthWindowSeconds"] = "60",
            ["RateLimiting:UseForwardedFor"] = "true",
        };

        if (IsSqlServer)
        {
            var baseConnection = Environment.GetEnvironmentVariable(ConnectionVariable)
                ?? throw new InvalidOperationException($"{ConnectionVariable} must be set when {ProviderVariable}=SqlServer.");
            _sqlServerConnectionString = new SqlConnectionStringBuilder(baseConnection)
            {
                InitialCatalog = $"TaskManagerTests_{Guid.NewGuid():N}",
            }.ConnectionString;
            _settings["Database:Provider"] = "SqlServer";
            _settings["Database:Schema"] = "Migrate";
            _settings["ConnectionStrings:Default"] = _sqlServerConnectionString;
        }
        else
        {
            _sqliteConnection = new SqliteConnection("DataSource=:memory:");
            _sqliteConnection.Open();
            _settings["Database:Provider"] = "Sqlite";
            _settings["Database:Schema"] = "EnsureCreated";
            _settings["ConnectionStrings:Default"] = "DataSource=:memory:"; // replaced by the kept-open connection below
        }

        foreach (var (key, value) in overrides)
        {
            _settings[key] = value;
        }
    }

    public bool IsSqlServer { get; }

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" so appsettings.Development.json is not loaded.
        builder.UseEnvironment("Testing");
        foreach (var (key, value) in _settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);

            if (_sqliteConnection is not null)
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options => options.UseSqlite(_sqliteConnection));
            }
        });
    }

    /// <summary>A client with its own cookie jar (one browser session), https base address, no redirects, and the CSRF header.</summary>
    public HttpClient CreateApiClient(bool handleCookies = true, bool withCsrfHeader = true)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri(BaseAddress),
            AllowAutoRedirect = false,
            HandleCookies = handleCookies,
        });

        if (withCsrfHeader)
        {
            client.DefaultRequestHeaders.Add(CsrfHeaderMiddleware.HeaderName, CsrfHeaderMiddleware.HeaderValue);
        }

        return client;
    }

    /// <summary>Runs <paramref name="action"/> against the app's database in a new DI scope.</summary>
    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
        {
            return;
        }

        if (_sqlServerConnectionString is not null)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(_sqlServerConnectionString).Options;
            using var db = new AppDbContext(options);
            db.Database.EnsureDeleted();
        }

        _sqliteConnection?.Dispose();
    }
}
