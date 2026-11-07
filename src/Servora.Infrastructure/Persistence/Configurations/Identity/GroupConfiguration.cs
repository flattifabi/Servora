using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Identity;

namespace Servora.Infrastructure.Persistence.Configurations.Identity;

/// <summary>EF Core mapping of <see cref="Group"/>.</summary>
internal sealed class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable("sys_user_group");
        builder.Property(g => g.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(g => g.Name).IsUnique();
        builder.Property(g => g.Description).HasMaxLength(2000);
        builder.Property(g => g.Email).HasMaxLength(320);
        builder.Property(g => g.DistinguishedName).HasMaxLength(1024);
        builder.Property(g => g.ExternalId).HasMaxLength(128);
        builder.HasIndex(g => g.ExternalId);
        builder.Property(g => g.CustomFieldsJson).HasMaxLength(32_000);
        builder.HasMany(g => g.Members).WithOne(m => m.Group).HasForeignKey(m => m.GroupId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(g => g.Roles).WithOne().HasForeignKey(r => r.GroupId).OnDelete(DeleteBehavior.Cascade);
    }
}
