using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Identity;

namespace Servora.Infrastructure.Persistence.Configurations.Identity;

/// <summary>EF Core mapping of <see cref="DirectoryGroupMapping"/>.</summary>
internal sealed class DirectoryGroupMappingConfiguration : IEntityTypeConfiguration<DirectoryGroupMapping>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DirectoryGroupMapping> builder)
    {
        builder.ToTable("sys_directory_group_map");
        builder.Property(m => m.DirectoryGroup).HasMaxLength(1024).IsRequired();
        builder.Property(m => m.Description).HasMaxLength(1000);
        builder.HasIndex(m => m.RoleId);
        builder.HasIndex(m => m.GroupId);
    }
}
