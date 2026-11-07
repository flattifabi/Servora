using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Tables;
using Servora.Domain.Analytics;
using Servora.Domain.Common;
using Servora.Domain.Identity;

namespace Servora.Infrastructure.Analytics;

/// <summary>
/// Seeds built-in sample reports (shared with everyone, owned by the bootstrap administrator) for every table that exists
/// at runtime, and pins the first ones to the administrator's personal dashboard. Idempotent per report name: reports
/// for modules installed later are added on the next start.
/// </summary>
/// <param name="registry">The table registry.</param>
public sealed class AnalyticsSeeder(ITableRegistry registry) : IDataSeeder
{
    private static readonly (string Name, string Description, string Table, string GroupBy, ReportChartType Chart)[] s_samples =
    [
        ("Users by source", "Local, directory (LDAP / AD) and Windows accounts.", TableNames.User, "Source", ReportChartType.Donut),
        ("Users by department", "Head count per department.", TableNames.User, "Department", ReportChartType.HorizontalBar),
        ("Groups by type", "Assignment and security groups.", TableNames.Group, "Type", ReportChartType.Donut),
        ("Configuration items by class", "CMDB composition per CI class.", TableNames.ConfigurationItem, "ClassName", ReportChartType.Bar),
        ("Configuration items by status", "Operational status of all configuration items.", TableNames.ConfigurationItem, "OperationalStatus", ReportChartType.Donut),
        ("Incidents by priority", "All incidents grouped by priority.", TableNames.Incident, "Priority", ReportChartType.Bar),
        ("Incidents opened per week", "Incident intake trend.", TableNames.Incident, "OpenedAt", ReportChartType.Line),
        ("Problems by priority", "All problems grouped by priority.", TableNames.Problem, "Priority", ReportChartType.Donut),
        ("Changes by state", "Change requests per state.", TableNames.ChangeRequest, "State", ReportChartType.Bar),
        ("Requested items by state", "Fulfilment pipeline of requested items.", TableNames.RequestItem, "State", ReportChartType.HorizontalBar),
        ("Knowledge articles by state", "Knowledge base lifecycle.", TableNames.KnowledgeArticle, "State", ReportChartType.Donut),
        ("Assets by state", "Asset inventory lifecycle.", TableNames.Asset, "State", ReportChartType.Donut),
    ];

    /// <inheritdoc />
    public int Order => 900;

    /// <inheritdoc />
    public async Task SeedAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        var admin = await db.Set<User>().AsNoTracking()
            .Where(u => u.NormalizedUserName == "ADMIN")
            .Select(u => new { u.Id, u.DisplayName })
            .FirstOrDefaultAsync(cancellationToken);
        if (admin is null)
        {
            return;
        }

        var existing = await db.Set<ReportDefinition>().AsNoTracking().Where(r => r.IsSystem).Select(r => r.Name).ToListAsync(cancellationToken);
        var added = new List<ReportDefinition>();
        foreach (var sample in s_samples.Where(s => !existing.Contains(s.Name)))
        {
            var table = await registry.GetTableAsync(sample.Table, cancellationToken);
            var field = table?.FindField(sample.GroupBy);
            if (table is null || field is null)
            {
                continue;
            }

            var report = new ReportDefinition
            {
                Name = sample.Name,
                Description = sample.Description,
                TableName = table.Name,
                GroupBy = field.Name,
                DateInterval = sample.Chart == ReportChartType.Line ? ReportDateInterval.Week : ReportDateInterval.Day,
                ChartType = sample.Chart,
                MaxGroups = 10,
                Visibility = ReportVisibility.Everyone,
                OwnerId = admin.Id,
                OwnerName = admin.DisplayName,
                IsSystem = true,
            };
            db.Set<ReportDefinition>().Add(report);
            added.Add(report);
        }

        if (added.Count == 0)
        {
            return;
        }

        var pins = await db.Set<DashboardPin>().Where(p => p.UserId == admin.Id).CountAsync(cancellationToken);
        if (existing.Count == 0 && pins == 0)
        {
            var position = 0;
            foreach (var report in added.Take(4))
            {
                db.Set<DashboardPin>().Add(new DashboardPin { UserId = admin.Id, ReportId = report.Id, Position = position++, PinnedAt = DateTimeOffset.UtcNow });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
