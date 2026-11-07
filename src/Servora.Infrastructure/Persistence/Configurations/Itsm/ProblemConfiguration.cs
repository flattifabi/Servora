using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Common;
using Servora.Domain.Itsm;

namespace Servora.Infrastructure.Persistence.Configurations.Itsm;

/// <summary>EF Core mapping of <see cref="Problem"/> (table <c>problem</c>).</summary>
public sealed class ProblemConfiguration : IEntityTypeConfiguration<Problem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Problem> builder)
    {
        builder.ConfigureTaskRecord(TableNames.Problem);
        builder.Property(p => p.Category).HasMaxLength(100);
        builder.Property(p => p.Workaround).HasMaxLength(8000);
        builder.Property(p => p.RootCause).HasMaxLength(8000);
        builder.Property(p => p.FixNotes).HasMaxLength(8000);
        builder.HasIndex(p => new { p.Active, p.State });
        builder.HasIndex(p => p.IsKnownError);
        builder.HasIndex(p => p.UpdatedAt);
    }
}
