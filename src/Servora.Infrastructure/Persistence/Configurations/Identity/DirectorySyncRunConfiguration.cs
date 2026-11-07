using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Identity;

namespace Servora.Infrastructure.Persistence.Configurations.Identity;

/// <summary>EF Core mapping of <see cref="DirectorySyncRun"/>.</summary>
internal sealed class DirectorySyncRunConfiguration : IEntityTypeConfiguration<DirectorySyncRun>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DirectorySyncRun> builder)
    {
        builder.ToTable("sys_directory_sync_run");
        builder.Property(r => r.StartedBy).HasMaxLength(256);
        builder.Property(r => r.Message).HasMaxLength(4000);
        builder.HasIndex(r => r.StartedAt);
    }
}
