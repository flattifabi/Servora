using Servora.Application.Approvals;
using Servora.Application.Changes;
using Servora.Application.Common;
using Servora.Application.Security;
using Servora.Domain.Approvals;
using Servora.Domain.Changes;
using Servora.Domain.Identity;

namespace Servora.Tests.Unit.ServiceManagement;

/// <summary>End-to-end tests of the change lifecycle with CAB approval and conflict detection (in-memory SQLite).</summary>
public sealed class ChangeLifecycleTests : IDisposable
{
    private readonly ServiceManagementTestHost _host = new();

    [Fact]
    public async Task NormalChange_IsAuthorizedByCab_AndClosed()
    {
        var manager = await _host.AddUserAsync("Carla Changemanager");
        var cabMember = await _host.AddUserAsync("Ben Board");
        await _host.AddGroupAsync(ChangeTables.ChangeAdvisoryBoardGroup, GroupType.Approval, cabMember);
        _host.User.Become(manager, "Carla Changemanager", Permissions.Changes.Read, Permissions.Changes.Create, Permissions.Changes.Write);
        var changes = _host.Get<IChangeService>();

        var created = await changes.CreateAsync(Plan(new ChangeEditModel { ShortDescription = "Upgrade firewall" }));
        Assert.True(created.Succeeded, created.Error);
        var id = created.Value;
        Assert.StartsWith("CHG", (await changes.GetAsync(id)).Value!.Number);

        Assert.True((await changes.TransitionAsync(id, ChangeState.Assess, new())).Succeeded);
        var noRisk = await changes.TransitionAsync(id, ChangeState.Authorize, new());
        Assert.Equal(ResultErrorKind.Validation, noRisk.ErrorKind);

        var risk = await changes.AssessRiskAsync(id, ChangeRiskCalculator.Questions.ToDictionary(q => q.Key, _ => 0));
        Assert.Equal(ChangeRisk.Low, risk.Value!.Risk);

        var authorize = await changes.TransitionAsync(id, ChangeState.Authorize, new("Ready for CAB"));
        Assert.True(authorize.Succeeded, authorize.Error);
        var detail = (await changes.GetAsync(id)).Value!;
        Assert.Equal(ChangeState.Authorize, detail.State);
        Assert.Equal(ApprovalStatus.Requested, detail.Approval);
        Assert.Equal(ChangeTables.ChangeAdvisoryBoardGroup, detail.ApprovalGroupName);

        // Scheduling without approval is refused.
        Assert.Equal(ResultErrorKind.Validation, (await changes.TransitionAsync(id, ChangeState.Scheduled, new())).ErrorKind);

        _host.User.Become(cabMember, "Ben Board", Permissions.Portal.Access);
        var inbox = _host.Get<IApprovalInboxService>();
        var approval = Assert.Single((await inbox.ListAsync(ApprovalInboxFilter.MyPending, new PageRequest())).Value!.Items);
        Assert.Equal($"/changes/{id}", approval.RecordRoute);
        Assert.True((await inbox.ApproveAsync(approval.Id, "Approved in CAB meeting")).Succeeded);

        _host.User.Become(manager, "Carla Changemanager", Permissions.Changes.Read, Permissions.Changes.Write);
        detail = (await changes.GetAsync(id)).Value!;
        Assert.Equal(ChangeState.Scheduled, detail.State);
        Assert.Equal(ApprovalStatus.Approved, detail.Approval);

        var task = await changes.AddTaskAsync(id, new ChangeTaskEditModel { ShortDescription = "Apply configuration" });
        Assert.True(task.Succeeded, task.Error);
        Assert.True((await changes.TransitionAsync(id, ChangeState.Implement, new())).Succeeded);
        Assert.True((await changes.TransitionAsync(id, ChangeState.Review, new())).Succeeded);

        var openTask = await changes.TransitionAsync(id, ChangeState.Closed, new(null, ChangeCloseCode.Successful, "Implemented as planned"));
        Assert.Equal(ResultErrorKind.Validation, openTask.ErrorKind);
        Assert.True((await changes.SetTaskStateAsync(task.Value, ChangeTaskState.Completed, "Done")).Succeeded);
        Assert.True((await changes.TransitionAsync(id, ChangeState.Closed, new(null, ChangeCloseCode.Successful, "Implemented as planned"))).Succeeded);

        detail = (await changes.GetAsync(id)).Value!;
        Assert.Equal(ChangeState.Closed, detail.State);
        Assert.NotNull(detail.ActualStartAt);
        Assert.NotNull(detail.ActualEndAt);
        Assert.False(detail.IsEditable);
        Assert.Equal(100, (await changes.GetStatsAsync()).Value!.SuccessRate);
    }

    [Fact]
    public async Task CabRejection_ReturnsChangeToAssess_AndHighRiskNeedsAllMembers()
    {
        var manager = await _host.AddUserAsync("Carla Changemanager");
        var first = await _host.AddUserAsync("Ben Board");
        var second = await _host.AddUserAsync("Bea Board");
        await _host.AddGroupAsync(ChangeTables.ChangeAdvisoryBoardGroup, GroupType.Approval, first, second);
        _host.User.Become(manager, "Carla Changemanager", Permissions.Changes.Read, Permissions.Changes.Create, Permissions.Changes.Write);
        var changes = _host.Get<IChangeService>();
        var id = (await changes.CreateAsync(Plan(new ChangeEditModel { ShortDescription = "Core network redesign" }))).Value;
        await changes.TransitionAsync(id, ChangeState.Assess, new());
        var risk = await changes.AssessRiskAsync(id, ChangeRiskCalculator.Questions.ToDictionary(q => q.Key, q => q.Options.Count - 1));
        Assert.Equal(ChangeRisk.VeryHigh, risk.Value!.Risk);
        Assert.True((await changes.TransitionAsync(id, ChangeState.Authorize, new())).Succeeded);

        var inbox = _host.Get<IApprovalInboxService>();
        _host.User.Become(first, "Ben Board");
        var approval = Assert.Single((await inbox.ListAsync(ApprovalInboxFilter.MyPending, new PageRequest())).Value!.Items);
        Assert.Equal(ApprovalMode.AllApprovers, approval.Mode);
        Assert.True((await inbox.ApproveAsync(approval.Id, null)).Succeeded);

        _host.User.Become(manager, "Carla Changemanager", Permissions.Changes.Read, Permissions.Changes.Write);
        Assert.Equal(ChangeState.Authorize, (await changes.GetAsync(id)).Value!.State);

        _host.User.Become(second, "Bea Board");
        approval = Assert.Single((await inbox.ListAsync(ApprovalInboxFilter.MyPending, new PageRequest())).Value!.Items);
        Assert.True((await inbox.RejectAsync(approval.Id, "Too risky before year end")).Succeeded);

        _host.User.Become(manager, "Carla Changemanager", Permissions.Changes.Read, Permissions.Changes.Write);
        var detail = (await changes.GetAsync(id)).Value!;
        Assert.Equal(ChangeState.Assess, detail.State);
        Assert.Equal(ApprovalStatus.Rejected, detail.Approval);
    }

    [Fact]
    public async Task OverlappingChangesOnTheSameCi_AreReportedAsConflicts()
    {
        var manager = await _host.AddUserAsync("Carla Changemanager");
        _host.User.Become(manager, "Carla Changemanager", Permissions.Changes.Read, Permissions.Changes.Create, Permissions.Changes.Write);
        var changes = _host.Get<IChangeService>();
        var ci = Guid.NewGuid();
        var start = _host.Clock.UtcNow.AddDays(3);

        var a = (await changes.CreateAsync(new ChangeEditModel { ShortDescription = "Patch server", ConfigurationItemId = ci, PlannedStartAt = start, PlannedEndAt = start.AddHours(4) })).Value;
        var b = (await changes.CreateAsync(new ChangeEditModel { ShortDescription = "Replace disk", ConfigurationItemId = ci, PlannedStartAt = start.AddHours(3), PlannedEndAt = start.AddHours(5) })).Value;
        await changes.CreateAsync(new ChangeEditModel { ShortDescription = "Later work", ConfigurationItemId = ci, PlannedStartAt = start.AddHours(4), PlannedEndAt = start.AddHours(6) });
        await changes.CreateAsync(new ChangeEditModel { ShortDescription = "Other CI", ConfigurationItemId = Guid.NewGuid(), PlannedStartAt = start, PlannedEndAt = start.AddHours(4) });

        var conflicts = (await changes.GetAsync(a)).Value!.Conflicts;
        var conflict = Assert.Single(conflicts);
        Assert.Equal(b, conflict.Id);

        var calendar = await changes.GetCalendarAsync(start.AddDays(-1), start.AddDays(1));
        Assert.Equal(4, calendar.Value!.Count);
    }

    [Fact]
    public async Task Permissions_AreEnforced()
    {
        var reader = await _host.AddUserAsync("Rita Reader");
        _host.User.Become(reader, "Rita Reader", Permissions.Changes.Read);
        var changes = _host.Get<IChangeService>();
        Assert.Equal(ResultErrorKind.Forbidden, (await changes.CreateAsync(new ChangeEditModel { ShortDescription = "x" })).ErrorKind);
        Assert.True((await changes.ListAsync(ChangeListFilter.All, new PageRequest())).Succeeded);

        _host.User.Become(reader, "Rita Reader");
        Assert.Equal(ResultErrorKind.Forbidden, (await changes.ListAsync(ChangeListFilter.All, new PageRequest())).ErrorKind);
    }

    private ChangeEditModel Plan(ChangeEditModel model)
    {
        model.PlannedStartAt = _host.Clock.UtcNow.AddDays(5);
        model.PlannedEndAt = _host.Clock.UtcNow.AddDays(5).AddHours(2);
        model.ImplementationPlan = "Steps";
        model.BackoutPlan = "Rollback";
        return model;
    }

    /// <inheritdoc />
    public void Dispose() => _host.Dispose();
}
