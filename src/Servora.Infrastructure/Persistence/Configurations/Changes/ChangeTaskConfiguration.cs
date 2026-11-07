using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Changes;
using Servora.Infrastructure.Persistence.Configurations.Itsm;

namespace Servora.Infrastructure.Persistence.Configurations.Changes;

/// <summary>EF Core mapping of <see cref="ChangeTask"/>.</summary>
internal sealed class ChangeTaskConfiguration : IEntityTypeConfiguration<ChangeTask>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ChangeTask> builder)
    {
        builder.ConfigureTaskRecord(ChangeTables.ChangeTask);
        builder.Property(t => t.CloseNotes).HasMaxLength(4000);
        builder.HasIndex(t => t.ChangeRequestId);
        builder.HasOne<ChangeRequest>().WithMany().HasForeignKey(t => t.ChangeRequestId).OnDelete(DeleteBehavior.Cascade);
    }
}
