using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Integration;

namespace Servora.Infrastructure.Persistence.Configurations.Integration;

/// <summary>EF Core mapping of <see cref="DataSource"/>.</summary>
internal sealed class DataSourceConfiguration : IEntityTypeConfiguration<DataSource>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DataSource> builder)
    {
        builder.ToTable("int_data_source");
        builder.Property(d => d.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(d => d.Name).IsUnique();
        builder.Property(d => d.Description).HasMaxLength(2000);
        builder.Property(d => d.BaseUrl).HasMaxLength(1000).IsRequired();
        builder.Property(d => d.ApiKeyHeaderName).HasMaxLength(100);
        builder.Property(d => d.BasicUserName).HasMaxLength(200);
        builder.Property(d => d.OAuthTokenUrl).HasMaxLength(1000);
        builder.Property(d => d.OAuthClientId).HasMaxLength(200);
        builder.Property(d => d.OAuthScope).HasMaxLength(500);
        builder.Property(d => d.DefaultHeadersJson).HasMaxLength(8000);
    }
}
