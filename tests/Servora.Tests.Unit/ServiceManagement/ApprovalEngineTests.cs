using Microsoft.EntityFrameworkCore;
using Servora.Application.Approvals;
using Servora.Application.Common;
using Servora.Application.Security;
using Servora.Domain.Approvals;
using Servora.Domain.Identity;

namespace Servora.Tests.Unit.ServiceManagement;

/// <summary>Tests for the approval engine, the approval service contract and the approval inbox (in-memory SQLite).</summary>
public sealed class ApprovalEngineTests : IDisposable
{
    private const string Table = "u_test_record";
    private readonly ServiceManagementTestHost _host = new();

    [Fact]
    public async Task GroupExpansion_DeduplicatesAndSkipsInactiveMembers()
    {
        var alice = await _host.AddUserAsync("Alice Approver");
        var bob = await _host.AddUserAsync("Bob Approver");
        var inactive = await _host.AddUserAsync("Ina Inactive", active: false);
        var group = await _host.AddGroupAsync("Board", GroupType.Approval, alice, bob, inactive);

        var approvers = await _host.Get<ApprovalEngine>().ResolveApproversAsync([alice], [group]);

        Assert.Equal(2, approvers.Count);
        Assert.Contains(approvers, a => a.UserId == alice && a.GroupId is null);
        Assert.Contains(approvers, a => a.UserId == bob && a.GroupId == group);
    }

    [Fact]
    public async Task Request_WithoutApprovers_IsInvalid()
    {
        var empty = await _host.AddGroupAsync("Empty board", GroupType.Approval);
        var result = await _host.Get<IApprovalService>().RequestAsync(new ApprovalRequest(Table, Guid.NewGuid(), [], [empty]));
        Assert.False(result.Succeeded);
        Assert.Equal(ResultErrorKind.Validation, result.ErrorKind);
    }

    [Fact]
    public async Task AnyPolicy_FirstApprovalDecidesTheRound()
    {
        var alice = await _host.AddUserAsync("Alice Approver");
        var bob = await _host.AddUserAsync("Bob Approver");
        var group = await _host.AddGroupAsync("Board", GroupType.Approval, alice, bob);
        var recordId = Guid.NewGuid();
        var service = _host.Get<IApprovalService>();

        Assert.True((await service.RequestAsync(new ApprovalRequest(Table, recordId, [], [group], ApprovalPolicy.AnyApprover, "Please approve"))).Succeeded);
        Assert.Equal(new ApprovalSummary(ApprovalState.Requested, 2, 0, 0), await service.GetSummaryAsync(Table, recordId));
        Assert.Equal(2, _host.Email.Messages.Count);
        Assert.Equal(1, await service.CountPendingForUserAsync(bob));

        _host.User.Become(alice, "Alice Approver", Permissions.Portal.Access);
        Assert.Equal(1, await service.CountPendingForUserAsync(alice));
        Assert.Null(await service.CountPendingForUserAsync(bob));
        var inbox = _host.Get<IApprovalInboxService>();
        var mine = await inbox.ListAsync(ApprovalInboxFilter.MyPending, new PageRequest());
        var approval = Assert.Single(mine.Value!.Items);
        Assert.True(approval.CanDecide);
        Assert.Equal("Board", approval.GroupName);

        Assert.True((await inbox.ApproveAsync(approval.Id, "Looks good")).Succeeded);

        _host.User.Become(bob, "Bob Approver", Permissions.Portal.Access);
        var bobsPending = await inbox.ListAsync(ApprovalInboxFilter.MyPending, new PageRequest());
        Assert.Empty(bobsPending.Value!.Items);

        _host.User.Permissions = [Permissions.All];
        var summary = await service.GetSummaryAsync(Table, recordId);
        Assert.Equal(ApprovalState.Approved, summary.State);
        Assert.Equal(0, summary.Pending);
    }

    [Fact]
    public async Task AllPolicy_NeedsEveryApprover_AndRejectionNeedsComment()
    {
        var alice = await _host.AddUserAsync("Alice Approver");
        var bob = await _host.AddUserAsync("Bob Approver");
        var recordId = Guid.NewGuid();
        var engine = _host.Get<ApprovalEngine>();
        Assert.True((await engine.RequestAsync(new ApprovalRequest(Table, recordId, [alice, bob], [], ApprovalPolicy.AllApprovers))).Succeeded);

        await using var db = _host.Database.CreateDbContext();
        var rows = await db.Set<Approval>().AsNoTracking().Where(a => a.RecordId == recordId).ToListAsync();

        _host.User.Become(alice, "Alice Approver");
        var first = await engine.DecideAsync(rows.Single(r => r.ApproverId == alice).Id, true, null, allowOnBehalf: false);
        Assert.Equal(ApprovalStatus.Requested, first.Value);

        _host.User.Become(bob, "Bob Approver");
        var noComment = await engine.DecideAsync(rows.Single(r => r.ApproverId == bob).Id, false, " ", allowOnBehalf: false);
        Assert.Equal(ResultErrorKind.Validation, noComment.ErrorKind);

        var rejected = await engine.DecideAsync(rows.Single(r => r.ApproverId == bob).Id, false, "Budget exhausted", allowOnBehalf: false);
        Assert.Equal(ApprovalStatus.Rejected, rejected.Value);
        Assert.Equal(ApprovalState.Rejected, (await engine.GetSummaryAsync(Table, recordId)).State);
    }

    [Fact]
    public async Task OnlyTheApproverOrAnAdministratorCanDecide()
    {
        var alice = await _host.AddUserAsync("Alice Approver");
        var mallory = await _host.AddUserAsync("Mallory Other");
        var recordId = Guid.NewGuid();
        Assert.True((await _host.Get<ApprovalEngine>().RequestAsync(new ApprovalRequest(Table, recordId, [alice], []))).Succeeded);
        await using var db = _host.Database.CreateDbContext();
        var approvalId = await db.Set<Approval>().Where(a => a.RecordId == recordId).Select(a => a.Id).SingleAsync();

        _host.User.Become(mallory, "Mallory Other", Permissions.Portal.Access);
        var inbox = _host.Get<IApprovalInboxService>();
        Assert.Equal(ResultErrorKind.Forbidden, (await inbox.ApproveAsync(approvalId, null)).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await inbox.GetForRecordAsync(Table, recordId)).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await _host.Get<IApprovalService>().RequestAsync(new ApprovalRequest(Table, recordId, [mallory], []))).ErrorKind);

        _host.User.Become(mallory, "Mallory Admin", Permissions.Approvals.Manage);
        Assert.True((await inbox.ApproveAsync(approvalId, "Approver on vacation")).Succeeded);
        Assert.Equal(ResultErrorKind.Conflict, (await inbox.ApproveAsync(approvalId, null)).ErrorKind);
    }

    [Fact]
    public async Task NewRound_WithdrawsPendingApprovals_AndCancelWorks()
    {
        var alice = await _host.AddUserAsync("Alice Approver");
        var recordId = Guid.NewGuid();
        var service = _host.Get<IApprovalService>();
        await service.RequestAsync(new ApprovalRequest(Table, recordId, [alice], []));
        await service.RequestAsync(new ApprovalRequest(Table, recordId, [alice], []));

        await using (var db = _host.Database.CreateDbContext())
        {
            var states = await db.Set<Approval>().Where(a => a.RecordId == recordId).Select(a => a.State).ToListAsync();
            Assert.Equal(2, states.Count);
            Assert.Single(states, ApprovalRecordState.Cancelled);
        }

        Assert.True((await service.CancelAsync(Table, recordId)).Succeeded);
        Assert.Equal(ApprovalState.Cancelled, (await service.GetSummaryAsync(Table, recordId)).State);
    }

    /// <inheritdoc />
    public void Dispose() => _host.Dispose();
}
