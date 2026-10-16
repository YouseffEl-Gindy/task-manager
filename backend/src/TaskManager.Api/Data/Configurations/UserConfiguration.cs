using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManager.Api.Entities;

namespace TaskManager.Api.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public const int DisplayNameMaxLength = 50;

    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.DisplayName).IsRequired().HasMaxLength(DisplayNameMaxLength);
        builder.Property(u => u.CreatedAt).IsRequired();

        builder.HasMany(u => u.Tasks)
            .WithOne(t => t.User)
            .HasForeignKey(t => t.UserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
