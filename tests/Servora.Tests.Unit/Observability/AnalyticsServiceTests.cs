using Microsoft.EntityFrameworkCore;
using Servora.Application.Analytics;
using Servora.Application.Common;
using Servora.Application.Security;
using Servora.Domain.Analytics;
using Servora.Domain.Cmdb;
using Servora.Domain.Identity;
using Servora.Domain.Observability;
using Servora.Infrastructure.Analytics;
using Servora.Infrastructure.Placeholders;
using Servora.Infrastructure.Platform;

namespace Servora.Tests.Unit.Observability;

/// <summary>Tests for the executive dashboard and the sample report seeder.</summary>
public sealed class AnalyticsServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 5, 4, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Dashboard_RendersWithSeedData_AndDegradesGracefully()
    {
        await SeedAsync();
        var user = new TestCurrentUser(Permissions.Analytics.Read, Permissions.Identity.ReadUsers, Permissions.Cmdb.Read, Permissions.Observability.ReadAudit);

        var result = await CreateService(user).GetExecutiveDashboardAsync(30);

        Assert.True(result.Succeeded, result.Error);
        var dashboard = result.Value!;
        Assert.Equal(30, dashboard.PeriodDays);

        var users = dashboard.Kpis.Single(k => k.Key == "users.active");
        Assert.Equal(AnalyticsAvailability.Ready, users.Availability);
        Assert.Equal("5", users.Value);

        var signIns = dashboard.Kpis.Single(k => k.Key == "platform.signins");
        Assert.Equal("3", signIns.Value);

        var cmdb = dashboard.Kpis.Single(k => k.Key == "cmdb.total");
        Assert.Equal("3", cmdb.Value);
        Assert.Contains("CMDB", dashboard.InstalledModules);

        var bySource = dashboard.Chart(AnalyticsChartKeys.UsersBySource);
        Assert.Equal(AnalyticsAvailability.Ready, bySource.Availability);
        Assert.Equal(6, bySource.Slices.Sum(s => s.Value));

        var security = dashboard.Kpis.Single(k => k.Key == "security.events");
        Assert.Equal("1", security.Value);

        // Every widget is either computed or explains why it is not (module missing, restricted, no data).
        Assert.All(dashboard.Charts.Values.Where(c => c.Availability != AnalyticsAvailability.Ready), c => Assert.False(string.IsNullOrWhiteSpace(c.Message)));
        Assert.All(dashboard.Kpis.Where(k => k.Availability is AnalyticsAvailability.ModuleMissing or AnalyticsAvailability.Restricted), k => Assert.Equal("—", k.Value));
    }

    [Fact]
    public async Task Dashboard_RestrictsSectionsWithoutTablePermissions()
    {
        await SeedAsync();
        var user = new TestCurrentUser(Permissions.Analytics.Read);

        var dashboard = (await CreateService(user).GetExecutiveDashboardAsync()).Value!;

        Assert.Equal(AnalyticsAvailability.Restricted, dashboard.Chart(AnalyticsChartKeys.UsersBySource).Availability);
        Assert.Equal(AnalyticsAvailability.Restricted, dashboard.Kpis.Single(k => k.Key == "cmdb.total").Availability);
        Assert.Equal(AnalyticsAvailability.Restricted, dashboard.Kpis.Single(k => k.Key == "security.events").Availability);
    }

    [Fact]
    public async Task Dashboard_RequiresAnalyticsPermission()
    {
        var result = await CreateService(new TestCurrentUser()).GetExecutiveDashboardAsync();

        Assert.Equal(ResultErrorKind.Forbidden, result.ErrorKind);
    }

    [Fact]
    public async Task Seeder_CreatesSamplesForInstalledTablesOnly_AndIsIdempotent()
    {
        await using (var db = _database.Create())
        {
            db.Set<User>().Add(new User { UserName = "admin", NormalizedUserName = "ADMIN", DisplayName = "Administrator" });
            await db.SaveChangesAsync();
        }

        var registry = new ReflectionTableRegistry();
        var seeder = new AnalyticsSeeder(registry);
        await using (var db = _database.Create())
        {
            await seeder.SeedAsync(db, CancellationToken.None);
        }

        await using (var db = _database.Create())
        {
            await seeder.SeedAsync(db, CancellationToken.None);
        }

        await using (var check = _database.Create())
        {
            var reports = await check.Set<ReportDefinition>().AsNoTracking().ToListAsync();
            Assert.NotEmpty(reports);
            Assert.All(reports, r => Assert.True(r.IsSystem));
            Assert.Equal(reports.Count, reports.Select(r => r.Name).Distinct().Count());
            foreach (var report in reports)
            {
                Assert.NotNull(await registry.GetTableAsync(report.TableName));
            }

            Assert.InRange(await check.Set<DashboardPin>().CountAsync(), 1, 4);
        }
    }

    /// <inheritdoc />
    public void Dispose() => _database.Dispose();

    private AnalyticsService CreateService(TestCurrentUser user)
    {
        var registry = new ReflectionTableRegistry();
        var engine = new ReportEngine(_database, registry, new UnavailableRecordService(), new PassThroughConditionEvaluator(), user, _clock);
        return new AnalyticsService(user, _database, registry, engine, _clock);
    }

    private async Task SeedAsync()
    {
        await using var db = _database.Create();
        for (var i = 0; i < 6; i++)
        {
            db.Set<User>().Add(new User
            {
                UserName = "u" + i,
                NormalizedUserName = "U" + i,
                DisplayName = "User " + i,
                Source = i < 4 ? UserSource.Local : UserSource.Directory,
                IsActive = i != 5,
            });
        }

        db.Set<ConfigurationItem>().AddRange(
            new ConfigurationItem { Name = "srv-01", ClassName = "cmdb_ci_server", OperationalStatus = OperationalStatus.Operational },
            new ConfigurationItem { Name = "srv-02", ClassName = "cmdb_ci_server", OperationalStatus = OperationalStatus.Operational },
            new ConfigurationItem { Name = "app-01", ClassName = "cmdb_ci_appl", OperationalStatus = OperationalStatus.NonOperational });

        var sequence = 0;
        foreach (var (action, outcome, daysAgo) in new[]
                 {
                     ("login.success", AuditOutcome.Success, 1),
                     ("login.success", AuditOutcome.Success, 2),
                     ("login.success", AuditOutcome.Success, 3),
                     ("login.failed", AuditOutcome.Failure, 2),
                     ("login.success", AuditOutcome.Success, 90),
                 })
        {
            db.Set<AuditEvent>().Add(new AuditEvent
            {
                Sequence = ++sequence,
                OccurredAt = _clock.UtcNow.AddDays(-daysAgo),
                Category = "security",
                Action = action,
                Outcome = outcome,
                ActorName = "User " + sequence,
                ActorType = "user",
                Hash = new string('0', 64),
            });
        }

        await db.SaveChangesAsync();
    }
}
