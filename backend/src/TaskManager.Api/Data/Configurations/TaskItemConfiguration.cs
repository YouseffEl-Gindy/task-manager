using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManager.Api.Entities;

namespace TaskManager.Api.Data.Configurations;

public sealed class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2000;
    public const int StatusMaxLength = 20;
    public const string UserCreatedAtIndexName = "IX_Tasks_UserId_CreatedAt";

    private readonly string _caseInsensitiveCollation;

    public TaskItemConfiguration(string caseInsensitiveCollation)
    {
        _caseInsensitiveCollation = caseInsensitiveCollation;
    }

    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("Tasks");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(TitleMaxLength)
            .UseCollation(_caseInsensitiveCollation);

        builder.Property(t => t.Description)
            .HasMaxLength(DescriptionMaxLength)
            .UseCollation(_caseInsensitiveCollation);

        builder.Property(t => t.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(StatusMaxLength);

        builder.Property(t => t.Priority)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt).IsRequired();

        // The only index: serves the default list query and doubles as the UserId FK index.
        builder.HasIndex(t => new { t.UserId, t.CreatedAt }).HasDatabaseName(UserCreatedAtIndexName);
    }
}
