using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Identity;

namespace Servora.Infrastructure.Persistence.Configurations.Identity;

/// <summary>EF Core mapping of <see cref="User"/>.</summary>
internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("sys_user");
        builder.Property(u => u.UserName).HasMaxLength(256).IsRequired();
        builder.Property(u => u.NormalizedUserName).HasMaxLength(256).IsRequired();
        builder.HasIndex(u => u.NormalizedUserName).IsUnique();
        builder.Property(u => u.DisplayName).HasMaxLength(256).IsRequired();
        builder.Property(u => u.FirstName).HasMaxLength(128);
        builder.Property(u => u.LastName).HasMaxLength(128);
        builder.Property(u => u.Email).HasMaxLength(320);
        builder.HasIndex(u => u.Email);
        builder.Property(u => u.Phone).HasMaxLength(64);
        builder.Property(u => u.Title).HasMaxLength(128);
        builder.Property(u => u.Department).HasMaxLength(128);
        builder.Property(u => u.Location).HasMaxLength(128);
        builder.Property(u => u.ExternalId).HasMaxLength(128);
        builder.HasIndex(u => u.ExternalId);
        builder.Property(u => u.DistinguishedName).HasMaxLength(1024);
        builder.Property(u => u.PasswordHash).HasMaxLength(512);
        builder.Property(u => u.SecurityStamp).HasMaxLength(64).IsRequired();
        builder.Property(u => u.TimeZone).HasMaxLength(64);
        builder.Property(u => u.Theme).HasMaxLength(16);
        builder.Property(u => u.CustomFieldsJson).HasMaxLength(32_000);
        builder.HasMany(u => u.Roles).WithOne().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(u => u.Groups).WithOne(m => m.User).HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
