using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TaskManager.Api.Data;

/// <summary>
/// Used only by the <c>dotnet ef</c> tools. Builds the SQL Server model; it never connects, so the
/// placeholder connection string is not a secret and is never used (SPEC 6.0).
/// </summary>
public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=TaskManager_DesignTime;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        return new AppDbContext(options);
    }
}
