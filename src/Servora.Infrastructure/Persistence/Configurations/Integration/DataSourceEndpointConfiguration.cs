using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Integration;

namespace Servora.Infrastructure.Persistence.Configurations.Integration;

/// <summary>EF Core mapping of <see cref="DataSourceEndpoint"/>.</summary>
internal sealed class DataSourceEndpointConfiguration : IEntityTypeConfiguration<DataSourceEndpoint>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DataSourceEndpoint> builder)
    {
        builder.ToTable("int_data_source_endpoint");
        builder.Property(e => e.Key).HasMaxLength(100).IsRequired();
        builder.HasIndex(e => new { e.DataSourceId, e.Key }).IsUnique();
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.Method).HasMaxLength(10).IsRequired();
        builder.Property(e => e.PathTemplate).HasMaxLength(1000).IsRequired();
        builder.Property(e => e.ParametersJson).HasMaxLength(16_000).IsRequired();
        builder.Property(e => e.BodyTemplate).HasMaxLength(16_000);
        builder.Property(e => e.ResponsePath).HasMaxLength(300);
        builder.HasOne<DataSource>().WithMany().HasForeignKey(e => e.DataSourceId).OnDelete(DeleteBehavior.Cascade);
    }
}
