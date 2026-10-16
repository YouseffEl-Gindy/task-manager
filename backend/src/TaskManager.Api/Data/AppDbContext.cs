using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Data.Configurations;
using TaskManager.Api.Data.Conventions;
using TaskManager.Api.Entities;

namespace TaskManager.Api.Data;

/// <summary>
/// Identity user tables (no roles), tasks, and the Data Protection key ring.
/// Provider-specific settings are chosen with <c>Database.IsSqlServer()</c> (SPEC 6.0).
/// </summary>
public sealed class AppDbContext : IdentityUserContext<User, Guid>, IDataProtectionKeyContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<TaskItem> Tasks { get; set; } = null!;

    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Both providers return Kind=Unspecified; mark every DateTime/DateTime? read from the database as UTC.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        var collation = ProviderCollations.CaseInsensitiveFor(Database);
        builder.ApplyConfiguration(new UserConfiguration());
        builder.ApplyConfiguration(new TaskItemConfiguration(collation));
    }
}
