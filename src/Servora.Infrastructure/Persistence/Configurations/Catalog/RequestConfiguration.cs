using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Catalog;
using Servora.Domain.Common;
using Servora.Infrastructure.Persistence.Configurations.Itsm;

namespace Servora.Infrastructure.Persistence.Configurations.Catalog;

/// <summary>EF Core mapping of <see cref="Request"/>.</summary>
internal sealed class RequestConfiguration : IEntityTypeConfiguration<Request>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Request> builder)
    {
        builder.ConfigureTaskRecord(TableNames.Request);
        builder.Property(r => r.TotalPrice).HasPrecision(18, 2);
        builder.HasIndex(r => r.RequestedForId);
        builder.HasIndex(r => r.State);
    }
}
