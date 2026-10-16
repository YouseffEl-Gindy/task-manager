using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Data;
using TaskManager.Api.Entities;

namespace TaskManager.Api.UnitTests.TestSupport;

/// <summary>A private SQLite in-memory database (kept-open connection) with the schema created from the model.</summary>
public sealed class SqliteTestDatabase : IDisposable
{
    public SqliteTestDatabase()
    {
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();
        Options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(Connection).Options;
        using var db = CreateContext();
        db.Database.EnsureCreated();
    }

    public SqliteConnection Connection { get; }

    public DbContextOptions<AppDbContext> Options { get; }

    public AppDbContext CreateContext() => new(Options);

    /// <summary>Inserts a user row directly (no Identity) and returns its generated id.</summary>
    public async Task<Guid> AddUserAsync(string? email = null)
    {
        email ??= $"user-{Guid.NewGuid():N}@example.com";
        await using var db = CreateContext();
        var user = new User
        {
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            DisplayName = "Test User",
            CreatedAt = DateTime.UtcNow,
            SecurityStamp = Guid.NewGuid().ToString(),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    public void Dispose() => Connection.Dispose();
}
