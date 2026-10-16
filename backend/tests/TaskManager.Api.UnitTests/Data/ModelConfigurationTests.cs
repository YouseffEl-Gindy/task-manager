using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using TaskManager.Api.Data;
using TaskManager.Api.Data.Conventions;
using TaskManager.Api.Entities;
using TaskManager.Api.UnitTests.TestSupport;

namespace TaskManager.Api.UnitTests.Data;

/// <summary>The per-provider model rules of SPEC 6.0. Building the SQL Server model never connects.</summary>
public sealed class ModelConfigurationTests
{
    private static IModel DesignModel(AppDbContext db) => db.GetService<IDesignTimeModel>().Model;

    private static AppDbContext SqlServerContext() => new AppDbContextDesignTimeFactory().CreateDbContext([]);

    [Fact]
    public void SqlServer_TitleAndDescription_UseCaseInsensitiveCollation()
    {
        using var db = SqlServerContext();
        var task = DesignModel(db).FindEntityType(typeof(TaskItem))!;

        Assert.Equal("Latin1_General_100_CI_AS", task.FindProperty(nameof(TaskItem.Title))!.GetCollation());
        Assert.Equal("Latin1_General_100_CI_AS", task.FindProperty(nameof(TaskItem.Description))!.GetCollation());
    }

    [Fact]
    public void Sqlite_TitleAndDescription_UseNocaseCollation()
    {
        using var database = new SqliteTestDatabase();
        using var db = database.CreateContext();
        var task = DesignModel(db).FindEntityType(typeof(TaskItem))!;

        Assert.Equal("NOCASE", task.FindProperty(nameof(TaskItem.Title))!.GetCollation());
        Assert.Equal("NOCASE", task.FindProperty(nameof(TaskItem.Description))!.GetCollation());
    }

    [Fact]
    public void Tasks_HaveMaxLengths_StringStatus_IntPriority()
    {
        using var db = SqlServerContext();
        var task = db.Model.FindEntityType(typeof(TaskItem))!;

        Assert.Equal("Tasks", task.GetTableName());
        Assert.Equal(200, task.FindProperty(nameof(TaskItem.Title))!.GetMaxLength());
        Assert.False(task.FindProperty(nameof(TaskItem.Title))!.IsNullable);
        Assert.Equal(2000, task.FindProperty(nameof(TaskItem.Description))!.GetMaxLength());
        Assert.Equal(20, task.FindProperty(nameof(TaskItem.Status))!.GetMaxLength());
        Assert.Equal(typeof(string), task.FindProperty(nameof(TaskItem.Status))!.GetTypeMapping().Converter!.ProviderClrType);
        Assert.Equal(typeof(int), task.FindProperty(nameof(TaskItem.Priority))!.GetTypeMapping().Converter!.ProviderClrType);
        Assert.Equal("nvarchar(20)", task.FindProperty(nameof(TaskItem.Status))!.GetColumnType());
        Assert.Equal("int", task.FindProperty(nameof(TaskItem.Priority))!.GetColumnType());
    }

    [Fact]
    public void Tasks_HaveOnlyTheUserIdCreatedAtIndex()
    {
        using var db = SqlServerContext();
        var task = db.Model.FindEntityType(typeof(TaskItem))!;

        var index = Assert.Single(task.GetIndexes());
        Assert.Equal("IX_Tasks_UserId_CreatedAt", index.GetDatabaseName());
        Assert.Equal([nameof(TaskItem.UserId), nameof(TaskItem.CreatedAt)], index.Properties.Select(p => p.Name));
    }

    [Fact]
    public void Tasks_CascadeDeleteWithUser()
    {
        using var db = SqlServerContext();
        var fk = Assert.Single(db.Model.FindEntityType(typeof(TaskItem))!.GetForeignKeys());

        Assert.Equal(typeof(User), fk.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Cascade, fk.DeleteBehavior);
    }

    [Fact]
    public void User_DisplayNameIsRequiredWithMax50()
    {
        using var db = SqlServerContext();
        var displayName = db.Model.FindEntityType(typeof(User))!.FindProperty(nameof(User.DisplayName))!;

        Assert.False(displayName.IsNullable);
        Assert.Equal(50, displayName.GetMaxLength());
    }

    [Theory]
    [InlineData(typeof(TaskItem), nameof(TaskItem.CreatedAt))]
    [InlineData(typeof(TaskItem), nameof(TaskItem.UpdatedAt))]
    [InlineData(typeof(TaskItem), nameof(TaskItem.DueDate))]
    [InlineData(typeof(User), nameof(User.CreatedAt))]
    public void EveryDateTimeProperty_UsesTheUtcConverter(Type entity, string property)
    {
        using var db = SqlServerContext();
        var converter = db.Model.FindEntityType(entity)!.FindProperty(property)!.GetValueConverter();

        Assert.IsType<UtcDateTimeConverter>(converter);
    }

    [Fact]
    public void NoAppOwnedColumnUsesDateTimeOffset()
    {
        using var db = SqlServerContext();
        var offsetProperties = db.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties().Select(p => (Entity: e.ClrType, Property: p)))
            .Where(x => x.Property.ClrType == typeof(DateTimeOffset) || x.Property.ClrType == typeof(DateTimeOffset?))
            .Select(x => $"{x.Entity.Name}.{x.Property.Name}")
            .ToList();

        // LockoutEnd comes from IdentityUser and is the one allowed exception (SPEC 6.0).
        Assert.Equal([$"{nameof(User)}.{nameof(User.LockoutEnd)}"], offsetProperties);
    }

    [Fact]
    public void GuidKeys_AreGeneratedByEf()
    {
        using var db = SqlServerContext();

        Assert.Equal(ValueGenerated.OnAdd, db.Model.FindEntityType(typeof(TaskItem))!.FindProperty(nameof(TaskItem.Id))!.ValueGenerated);
        Assert.Equal(ValueGenerated.OnAdd, db.Model.FindEntityType(typeof(User))!.FindProperty(nameof(User.Id))!.ValueGenerated);
    }

    [Fact]
    public void UtcDateTimeConverter_MarksReadValuesAsUtc_AndNormalizesLocalOnWrite()
    {
        var unspecified = new DateTime(2026, 10, 12, 15, 0, 0, DateTimeKind.Unspecified);

        var read = UtcDateTimeConverter.FromStore(unspecified);
        Assert.Equal(DateTimeKind.Utc, read.Kind);
        Assert.Equal(unspecified.Ticks, read.Ticks);

        var local = new DateTime(2026, 10, 12, 15, 0, 0, DateTimeKind.Local);
        Assert.Equal(local.ToUniversalTime(), UtcDateTimeConverter.ToStore(local));
    }

    [Fact]
    public async Task Sqlite_ValuesReadBack_HaveUtcKind_AndForeignKeysAreOn()
    {
        using var database = new SqliteTestDatabase();
        var userId = await database.AddUserAsync();
        await using (var db = database.CreateContext())
        {
            db.Tasks.Add(new TaskItem
            {
                UserId = userId,
                Title = "t",
                DueDate = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        await using (var db = database.CreateContext())
        {
            var task = await db.Tasks.AsNoTracking().SingleAsync();
            Assert.Equal(DateTimeKind.Utc, task.CreatedAt.Kind);
            Assert.Equal(DateTimeKind.Utc, task.DueDate!.Value.Kind);
            Assert.NotEqual(Guid.Empty, task.Id);

            // Cascade relies on PRAGMA foreign_keys=ON, which EF Core turns on when it opens the connection.
            db.Users.Remove(await db.Users.SingleAsync());
            await db.SaveChangesAsync();
            Assert.Equal(0, await db.Tasks.CountAsync());
        }
    }
}
