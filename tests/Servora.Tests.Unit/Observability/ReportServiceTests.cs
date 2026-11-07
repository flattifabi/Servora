using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Servora.Application.Analytics;
using Servora.Application.Common;
using Servora.Application.Security;
using Servora.Domain.Analytics;
using Servora.Domain.Common;
using Servora.Domain.Identity;
using Servora.Domain.Observability;
using Servora.Infrastructure.Observability;
using Servora.Infrastructure.Placeholders;
using Servora.Infrastructure.Platform;

namespace Servora.Tests.Unit.Observability;

/// <summary>Tests for saving, sharing, running and pinning reports (report builder).</summary>
public sealed class ReportServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 5, 4, 10, 0, 0, TimeSpan.Zero));
    private readonly TestPermissionService _permissions = new();
    private readonly TestCurrentUser _owner = new(Permissions.Analytics.Read, Permissions.Analytics.ManageReports, Permissions.Identity.ReadUsers);

    [Fact]
    public async Task SavedGroupedReport_RendersGroupedChartAndIsAudited()
    {
        await SeedUsersAsync();
        var service = CreateService(_owner);

        var saved = await service.SaveAsync(UsersBySource("Users by source", ReportVisibility.Private));
        Assert.True(saved.Succeeded, saved.Error);

        var run = await service.RunSavedAsync(saved.Value);

        Assert.True(run.Succeeded, run.Error);
        Assert.Equal(ReportChartType.Bar, run.Value!.ChartType);
        Assert.Equal(2, run.Value.Rows.Count);
        Assert.Equal(4, run.Value.Rows.Single(r => r.Label == "Local").Value);
        Assert.Equal(2, run.Value.Rows.Single(r => r.Label != "Local").Value);

        await using var db = _database.Create();
        Assert.True(await db.Set<AuditEvent>().AnyAsync(a => a.Action == "report.created" && a.TargetId == saved.Value.ToString()));
    }

    [Fact]
    public async Task RoleSharedReport_IsVisibleToRoleMembersOnly_AndOnlyOwnerEdits()
    {
        await SeedUsersAsync();
        await SeedRoleAsync("service_desk");
        var saved = await CreateService(_owner).SaveAsync(UsersBySource("Shared report", ReportVisibility.Role, "service_desk"));
        Assert.True(saved.Succeeded, saved.Error);

        var member = new TestCurrentUser(Permissions.Analytics.Read, Permissions.Analytics.ManageReports, Permissions.Identity.ReadUsers);
        _permissions.Assign(member.UserId!.Value, "service_desk");
        var outsider = new TestCurrentUser(Permissions.Analytics.Read, Permissions.Identity.ReadUsers);

        Assert.True((await CreateService(member).GetAsync(saved.Value)).Succeeded);
        Assert.True((await CreateService(member).RunSavedAsync(saved.Value)).Succeeded);
        Assert.Equal(ResultErrorKind.NotFound, (await CreateService(outsider).GetAsync(saved.Value)).ErrorKind);

        var edit = await CreateService(member).SaveAsync(UsersBySource("Hijacked", ReportVisibility.Everyone) with { Id = saved.Value });
        Assert.Equal(ResultErrorKind.Forbidden, edit.ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await CreateService(member).DeleteAsync(saved.Value)).ErrorKind);

        var shared = await CreateService(member).ListAsync(ReportListScope.Shared, new PageRequest());
        Assert.Single(shared.Value!.Items);
        Assert.False(shared.Value.Items[0].IsMine);
    }

    [Fact]
    public async Task Save_RequiresManagePermission_AndExistingRole()
    {
        var reader = new TestCurrentUser(Permissions.Analytics.Read, Permissions.Identity.ReadUsers);
        Assert.Equal(ResultErrorKind.Forbidden, (await CreateService(reader).SaveAsync(UsersBySource("x", ReportVisibility.Private))).ErrorKind);

        var invalidRole = await CreateService(_owner).SaveAsync(UsersBySource("x", ReportVisibility.Role, "does_not_exist"));
        Assert.Equal(ResultErrorKind.Validation, invalidRole.ErrorKind);

        var noTableAccess = new TestCurrentUser(Permissions.Analytics.Read, Permissions.Analytics.ManageReports);
        Assert.Equal(ResultErrorKind.Forbidden, (await CreateService(noTableAccess).SaveAsync(UsersBySource("x", ReportVisibility.Private))).ErrorKind);
    }

    [Fact]
    public async Task PinnedReport_AppearsOnPersonalDashboardWithResult_UntilUnpinned()
    {
        await SeedUsersAsync();
        var service = CreateService(_owner);
        var saved = await service.SaveAsync(UsersBySource("Pinned", ReportVisibility.Private));

        Assert.True((await service.PinAsync(saved.Value!, wide: true)).Succeeded);
        var pinned = await service.GetPinnedAsync();

        var tile = Assert.Single(pinned.Value!);
        Assert.True(tile.Wide);
        Assert.True(tile.Report.IsPinned);
        Assert.NotNull(tile.Result);
        Assert.Equal(6, tile.Result!.Total);

        Assert.True((await service.UnpinAsync(saved.Value)).Succeeded);
        Assert.Empty((await service.GetPinnedAsync()).Value!);
    }

    [Fact]
    public async Task DeletingReport_RemovesPins()
    {
        await SeedUsersAsync();
        var service = CreateService(_owner);
        var saved = await service.SaveAsync(UsersBySource("Temporary", ReportVisibility.Private));
        await service.PinAsync(saved.Value);

        Assert.True((await service.DeleteAsync(saved.Value)).Succeeded);

        await using var db = _database.Create();
        Assert.False(await db.Set<DashboardPin>().AnyAsync());
        Assert.False(await db.Set<ReportDefinition>().AnyAsync());
    }

    /// <inheritdoc />
    public void Dispose() => _database.Dispose();

    private static ReportSaveRequest UsersBySource(string name, ReportVisibility visibility, string? role = null) =>
        new(null, name, "Accounts per source.", new ReportSpec { TableName = TableNames.User, GroupBy = "Source", ChartType = ReportChartType.Bar }, visibility, role);

    private ReportService CreateService(TestCurrentUser user)
    {
        var registry = new ReflectionTableRegistry();
        var engine = new ReportEngine(_database, registry, new UnavailableRecordService(), new PassThroughConditionEvaluator(), user, _clock);
        var audit = new AuditService(_database, user, _clock, NullLogger<AuditService>.Instance);
        return new ReportService(user, _permissions, _database, registry, engine, audit, _clock);
    }

    private async Task SeedRoleAsync(string name)
    {
        await using var db = _database.Create();
        db.Set<Role>().Add(new Role { Name = name, Label = "Service desk" });
        await db.SaveChangesAsync();
    }

    private async Task SeedUsersAsync()
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
            });
        }

        await db.SaveChangesAsync();
    }
}
