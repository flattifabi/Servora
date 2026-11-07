using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Itsm;
using Servora.Application.Security;
using Servora.Domain.Activity;
using Servora.Domain.Itsm;

namespace Servora.Tests.Unit.Itsm;

/// <summary>End-to-end tests of <see cref="IncidentService"/>, <see cref="ProblemService"/> and <see cref="MyWorkService"/> on SQLite.</summary>
public sealed class IncidentServiceTests
{
    [Fact]
    public async Task Create_AllocatesNumberAndCalculatesPriority()
    {
        await using var host = await ItsmTestHost.CreateAsync();

        var created = await host.Incidents.CreateAsync(new IncidentInput { ShortDescription = "Email down", Impact = Impact.High, Urgency = Urgency.High });

        Assert.True(created.Succeeded, created.Error);
        var details = (await host.Incidents.GetAsync(created.Value)).Value!;
        Assert.Equal("INC0001001", details.Number);
        Assert.Equal(Priority.Critical, details.Priority);
        Assert.Equal(IncidentState.New, details.State);
        Assert.Equal(host.User.UserId, details.Fields.CallerId);
    }

    [Fact]
    public async Task Create_RequiresShortDescriptionAndPermission()
    {
        await using var host = await ItsmTestHost.CreateAsync();

        var invalid = await host.Incidents.CreateAsync(new IncidentInput { ShortDescription = " " });
        Assert.Equal(ResultErrorKind.Validation, invalid.ErrorKind);

        host.User.Grant(Permissions.Incidents.Read);
        var forbidden = await host.Incidents.CreateAsync(new IncidentInput { ShortDescription = "x" });
        Assert.Equal(ResultErrorKind.Forbidden, forbidden.ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await host.Problems.ListAsync(ProblemQuickFilter.All, new PageRequest())).ErrorKind);
    }

    [Fact]
    public async Task Lifecycle_HoldResolveReopenClose()
    {
        await using var host = await ItsmTestHost.CreateAsync();
        var id = (await host.Incidents.CreateAsync(new IncidentInput { ShortDescription = "VPN drops" })).Value;

        Assert.True((await host.Incidents.StartWorkAsync(id)).Succeeded);
        Assert.Equal(host.User.UserId, (await host.Incidents.GetAsync(id)).Value!.Fields.AssignedToId);

        Assert.True((await host.Incidents.PutOnHoldAsync(id, OnHoldReason.AwaitingCaller, "Asked for logs")).Succeeded);
        Assert.Equal(IncidentState.OnHold, (await host.Incidents.GetAsync(id)).Value!.State);

        var noNotes = await host.Incidents.ResolveAsync(id, IncidentResolutionCode.SolvedRemotely, "  ");
        Assert.Equal(ResultErrorKind.Validation, noNotes.ErrorKind);

        Assert.True((await host.Incidents.ResolveAsync(id, IncidentResolutionCode.SolvedRemotely, "Reinstalled client")).Succeeded);
        var resolved = (await host.Incidents.GetAsync(id)).Value!;
        Assert.Equal(IncidentState.Resolved, resolved.State);
        Assert.NotNull(resolved.ResolvedAt);
        Assert.Null(resolved.OnHoldReason);

        Assert.Equal(ResultErrorKind.Validation, (await host.Incidents.ReopenAsync(id, "")).ErrorKind);
        Assert.True((await host.Incidents.ReopenAsync(id, "Still failing")).Succeeded);
        var reopened = (await host.Incidents.GetAsync(id)).Value!;
        Assert.Equal(IncidentState.InProgress, reopened.State);
        Assert.Equal(1, reopened.ReopenCount);
        Assert.Null(reopened.ResolvedAt);

        Assert.Equal(ResultErrorKind.Validation, (await host.Incidents.CloseAsync(id)).ErrorKind);
        Assert.True((await host.Incidents.ResolveAsync(id, IncidentResolutionCode.SolvedPermanently, "Replaced router")).Succeeded);
        Assert.True((await host.Incidents.CloseAsync(id)).Succeeded);
        var closed = (await host.Incidents.GetAsync(id)).Value!;
        Assert.Equal(IncidentState.Closed, closed.State);
        Assert.False(closed.IsActive);

        var edit = await host.Incidents.UpdateAsync(id, closed.Version, closed.Fields);
        Assert.Equal(ResultErrorKind.Validation, edit.ErrorKind);

        await using var db = host.Create();
        var activity = await db.Set<ActivityEntry>().Where(a => a.RecordId == id).ToListAsync();
        Assert.Contains(activity, a => a.Type == ActivityType.WorkNote && a.Text == "Asked for logs");
        Assert.True(activity.Count(a => a.Type == ActivityType.System) >= 6);
    }

    [Fact]
    public async Task Update_TracksChangesAndDetectsConflicts()
    {
        await using var host = await ItsmTestHost.CreateAsync();
        var id = (await host.Incidents.CreateAsync(new IncidentInput { ShortDescription = "Slow ERP" })).Value;
        var details = (await host.Incidents.GetAsync(id)).Value!;

        var input = details.Fields;
        input.Impact = Impact.High;
        input.Urgency = Urgency.Medium;
        Assert.True((await host.Incidents.UpdateAsync(id, details.Version, input)).Succeeded);
        Assert.Equal(Priority.High, (await host.Incidents.GetAsync(id)).Value!.Priority);

        var stale = await host.Incidents.UpdateAsync(id, details.Version, input);
        Assert.Equal(ResultErrorKind.Conflict, stale.ErrorKind);

        await using var db = host.Create();
        var change = await db.Set<ActivityEntry>().SingleAsync(a => a.RecordId == id && a.Type == ActivityType.FieldChange);
        Assert.Contains("Priority", change.ChangesJson);
    }

    [Fact]
    public async Task List_FiltersSearchesAndPages()
    {
        await using var host = await ItsmTestHost.CreateAsync();
        for (var i = 0; i < 12; i++)
        {
            await host.Incidents.CreateAsync(new IncidentInput { ShortDescription = $"Printer {i}", Impact = i % 2 == 0 ? Impact.High : Impact.Low, Urgency = Urgency.High });
        }

        var vpn = (await host.Incidents.CreateAsync(new IncidentInput { ShortDescription = "VPN tunnel resets" })).Value;
        await host.Incidents.AssignToMeAsync(vpn);

        var page = (await host.Incidents.ListAsync(IncidentQuickFilter.AllActive, new PageRequest { Page = 2, PageSize = 5 })).Value!;
        Assert.Equal(13, page.TotalCount);
        Assert.Equal(5, page.Items.Count);

        var high = (await host.Incidents.ListAsync(IncidentQuickFilter.HighPriority, new PageRequest())).Value!;
        Assert.Equal(6, high.TotalCount);
        Assert.All(high.Items, i => Assert.Equal(Priority.Critical, i.Priority));

        var search = (await host.Incidents.ListAsync(IncidentQuickFilter.All, new PageRequest { Search = "vpn" })).Value!;
        Assert.Single(search.Items);

        var mine = (await host.Incidents.ListAsync(IncidentQuickFilter.MyOpen, new PageRequest())).Value!;
        Assert.Equal(vpn, Assert.Single(mine.Items).Id);

        var byNumber = (await host.Incidents.ListAsync(IncidentQuickFilter.All, new PageRequest { SortBy = "Number", SortDescending = true })).Value!;
        Assert.Equal("INC0001013", byNumber.Items[0].Number);

        var stats = (await host.Incidents.GetStatsAsync()).Value!;
        Assert.Equal(13, stats.Open);
        Assert.Equal(6, stats.Critical);
        Assert.Equal(12, stats.Unassigned);
    }

    [Fact]
    public async Task BulkAssign_UpdatesActiveIncidents()
    {
        await using var host = await ItsmTestHost.CreateAsync();
        var a = (await host.Incidents.CreateAsync(new IncidentInput { ShortDescription = "A" })).Value;
        var b = (await host.Incidents.CreateAsync(new IncidentInput { ShortDescription = "B" })).Value;

        var result = await host.Incidents.AssignAsync([a, b], null, host.User.UserId);

        Assert.Equal(2, result.Value);
        Assert.Equal(host.User.UserId, (await host.Incidents.GetAsync(b)).Value!.Fields.AssignedToId);
        Assert.Equal(ResultErrorKind.Validation, (await host.Incidents.AssignAsync([a], null, null)).ErrorKind);
        Assert.Equal(ResultErrorKind.Validation, (await host.Incidents.AssignAsync([a], null, Guid.NewGuid())).ErrorKind);
    }

    [Fact]
    public async Task CreateProblemFromIncident_CopiesFieldsAndLinks()
    {
        await using var host = await ItsmTestHost.CreateAsync();
        var incidentId = (await host.Incidents.CreateAsync(new IncidentInput { ShortDescription = "Deadlocks in ERP", Category = "Database", Impact = Impact.High, Urgency = Urgency.High })).Value;

        var problemId = (await host.Problems.CreateFromIncidentAsync(incidentId)).Value;

        var problem = (await host.Problems.GetAsync(problemId)).Value!;
        Assert.Equal("PRB0001001", problem.Number);
        Assert.Equal("Deadlocks in ERP", problem.Fields.ShortDescription);
        Assert.Equal("Database", problem.Fields.Category);
        Assert.Equal(Priority.Critical, problem.Priority);
        Assert.Equal(1, problem.IncidentCount);
        Assert.Equal(problemId, (await host.Incidents.GetAsync(incidentId)).Value!.ProblemId);
        Assert.Equal(ResultErrorKind.Conflict, (await host.Problems.CreateFromIncidentAsync(incidentId)).ErrorKind);

        Assert.True((await host.Problems.UnlinkIncidentAsync(problemId, incidentId)).Succeeded);
        Assert.Equal(1, (await host.Problems.LinkIncidentsAsync(problemId, [incidentId])).Value);
    }

    [Fact]
    public async Task ProblemLifecycle_RequiresRootCauseBeforeFix()
    {
        await using var host = await ItsmTestHost.CreateAsync();
        var id = (await host.Problems.CreateAsync(new ProblemInput { ShortDescription = "Recurring VPN drops" })).Value;

        Assert.True((await host.Problems.MoveToAsync(id, ProblemState.Assess)).Succeeded);
        Assert.True((await host.Problems.MoveToAsync(id, ProblemState.RootCauseAnalysis)).Succeeded);
        var blocked = await host.Problems.MoveToAsync(id, ProblemState.FixInProgress);
        Assert.Equal(ResultErrorKind.Validation, blocked.ErrorKind);
        Assert.Contains("root cause", blocked.Error);

        var details = (await host.Problems.GetAsync(id)).Value!;
        details.Fields.RootCause = "Client renegotiates IPv6 tunnel.";
        details.Fields.Workaround = "Disable IPv6 tunnel.";
        details.Fields.IsKnownError = true;
        Assert.True((await host.Problems.UpdateAsync(id, details.Version, details.Fields)).Succeeded);

        Assert.True((await host.Problems.MoveToAsync(id, ProblemState.FixInProgress)).Succeeded);
        Assert.True((await host.Problems.ResolveAsync(id, ProblemResolutionCode.FixApplied, "Client 6.2.3 deployed")).Succeeded);
        Assert.True((await host.Problems.CloseAsync(id)).Succeeded);
        Assert.Equal(ProblemState.Closed, (await host.Problems.GetAsync(id)).Value!.State);
    }

    [Fact]
    public async Task MyWork_ReturnsAssignedItemsAndTrend()
    {
        await using var host = await ItsmTestHost.CreateAsync();
        var id = (await host.Incidents.CreateAsync(new IncidentInput { ShortDescription = "Laptop broken", Impact = Impact.High, Urgency = Urgency.High })).Value;
        await host.Incidents.AssignToMeAsync(id);
        await host.Incidents.ResolveAsync((await host.Incidents.CreateAsync(new IncidentInput { ShortDescription = "Done" })).Value, IncidentResolutionCode.SolvedRemotely, "ok");

        var dashboard = (await host.MyWork.GetAsync()).Value!;

        Assert.Equal(1, dashboard.AssignedToMe);
        Assert.Equal(1, dashboard.CriticalOpen);
        Assert.Equal(id, Assert.Single(dashboard.Items).Id);
        Assert.Equal(14, dashboard.TrendLabels.Count);
        Assert.Equal(2, dashboard.OpenedPerDay.Sum());
        Assert.Equal(1, dashboard.ResolvedPerDay.Sum());
        Assert.NotEmpty(dashboard.RecentActivity);
    }
}
