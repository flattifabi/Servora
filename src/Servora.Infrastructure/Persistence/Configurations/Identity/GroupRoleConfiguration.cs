using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Identity;

namespace Servora.Infrastructure.Persistence.Configurations.Identity;

/// <summary>EF Core mapping of <see cref="GroupRole"/>.</summary>
internal sealed class GroupRoleConfiguration : IEntityTypeConfiguration<GroupRole>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<GroupRole> builder)
    {
        builder.ToTable("sys_group_has_role");
        builder.HasIndex(r => new { r.GroupId, r.RoleId }).IsUnique();
        builder.HasOne(r => r.Role).WithMany().HasForeignKey(r => r.RoleId).OnDelete(DeleteBehavior.Cascade);
    }
}
