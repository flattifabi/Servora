using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Integration;

namespace Servora.Infrastructure.Persistence.Configurations.Integration;

/// <summary>EF Core mapping of <see cref="DataSourceExecutionLog"/>.</summary>
internal sealed class DataSourceExecutionLogConfiguration : IEntityTypeConfiguration<DataSourceExecutionLog>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DataSourceExecutionLog> builder)
    {
        builder.ToTable("int_data_source_log");
        builder.Property(l => l.DataSourceName).HasMaxLength(200).IsRequired();
        builder.Property(l => l.EndpointKey).HasMaxLength(100).IsRequired();
        builder.Property(l => l.Method).HasMaxLength(10).IsRequired();
        builder.Property(l => l.Host).HasMaxLength(255);
        builder.Property(l => l.Error).HasMaxLength(2000);
        builder.Property(l => l.ActorName).HasMaxLength(256);
        builder.HasIndex(l => l.StartedAt);
        builder.HasIndex(l => new { l.DataSourceId, l.StartedAt });
    }
}
