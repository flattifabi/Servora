using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Api;

namespace Servora.Infrastructure.Persistence.Configurations.Api;

/// <summary>EF Core mapping of <see cref="ApiKeyUsageDay"/>.</summary>
internal sealed class ApiKeyUsageDayConfiguration : IEntityTypeConfiguration<ApiKeyUsageDay>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ApiKeyUsageDay> builder)
    {
        builder.ToTable("sys_api_key_usage");
        builder.HasIndex(u => new { u.ApiKeyId, u.Day }).IsUnique();
        builder.HasIndex(u => u.Day);
    }
}
