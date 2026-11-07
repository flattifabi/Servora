using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Identity;

namespace Servora.Infrastructure.Persistence.Configurations.Identity;

/// <summary>EF Core mapping of <see cref="GroupMember"/>.</summary>
internal sealed class GroupMemberConfiguration : IEntityTypeConfiguration<GroupMember>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<GroupMember> builder)
    {
        builder.ToTable("sys_user_grmember");
        builder.HasIndex(m => new { m.GroupId, m.UserId }).IsUnique();
    }
}
