using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Approvals;
using Servora.Domain.Common;

namespace Servora.Infrastructure.Persistence.Configurations.Approvals;

/// <summary>EF Core mapping of <see cref="Approval"/>.</summary>
internal sealed class ApprovalConfiguration : IEntityTypeConfiguration<Approval>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Approval> builder)
    {
        builder.ToTable(TableNames.Approval);
        builder.Property(a => a.TableName).HasMaxLength(100).IsRequired();
        builder.Property(a => a.RecordNumber).HasMaxLength(40);
        builder.Property(a => a.RecordSummary).HasMaxLength(300);
        builder.Property(a => a.RecordRoute).HasMaxLength(300);
        builder.Property(a => a.Reason).HasMaxLength(2000);
        builder.Property(a => a.Comments).HasMaxLength(4000);
        builder.HasIndex(a => new { a.TableName, a.RecordId });
        builder.HasIndex(a => new { a.ApproverId, a.State });
        builder.HasIndex(a => a.BatchId);
    }
}
