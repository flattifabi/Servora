using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Identity;

namespace Servora.Infrastructure.Persistence.Configurations.Identity;

/// <summary>EF Core mapping of <see cref="RolePermission"/>.</summary>
internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("sys_role_permission");
        builder.Property(p => p.Permission).HasMaxLength(200).IsRequired();
        builder.HasIndex(p => new { p.RoleId, p.Permission }).IsUnique();
    }
}
