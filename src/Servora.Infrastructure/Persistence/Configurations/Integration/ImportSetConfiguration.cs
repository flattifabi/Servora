using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Integration;

namespace Servora.Infrastructure.Persistence.Configurations.Integration;

/// <summary>EF Core mapping of <see cref="ImportSet"/>.</summary>
internal sealed class ImportSetConfiguration : IEntityTypeConfiguration<ImportSet>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ImportSet> builder)
    {
        builder.ToTable("int_import_set");
        builder.Property(i => i.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(i => i.Name).IsUnique();
        builder.Property(i => i.Description).HasMaxLength(2000);
        builder.Property(i => i.EndpointKey).HasMaxLength(100);
        builder.Property(i => i.ParametersJson).HasMaxLength(8000);
        builder.Property(i => i.RecordsPath).HasMaxLength(300);
        builder.Property(i => i.TargetTable).HasMaxLength(100).IsRequired();
        builder.Property(i => i.CoalesceField).HasMaxLength(100);
        builder.Property(i => i.FieldMapsJson).HasMaxLength(32_000).IsRequired();
        builder.Property(i => i.CronExpression).HasMaxLength(120);
        builder.Property(i => i.TimeZoneId).HasMaxLength(100);
        builder.HasIndex(i => new { i.ScheduleEnabled, i.NextRunAt });
    }
}
