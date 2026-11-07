using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Analytics;

namespace Servora.Infrastructure.Persistence.Configurations.Analytics;

/// <summary>EF Core mapping of <see cref="DashboardPin"/>.</summary>
internal sealed class DashboardPinConfiguration : IEntityTypeConfiguration<DashboardPin>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DashboardPin> builder)
    {
        builder.ToTable("sys_dashboard_pin");
        builder.HasIndex(p => new { p.UserId, p.ReportId }).IsUnique();
        builder.HasIndex(p => p.ReportId);
    }
}
