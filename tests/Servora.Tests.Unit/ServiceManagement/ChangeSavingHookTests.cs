using Servora.Application.Changes;
using Servora.Application.Events;
using Servora.Domain.Approvals;
using Servora.Domain.Changes;
using Servora.Domain.Itsm;

namespace Servora.Tests.Unit.ServiceManagement;

/// <summary>Tests of <see cref="ChangeSavingHook"/> (invariants for every writer of change requests and change tasks).</summary>
public sealed class ChangeSavingHookTests
{
    private readonly TestClock _clock = new();

    [Fact]
    public async Task Insert_CalculatesPriority_AndOpenedAt()
    {
        var change = new ChangeRequest { Impact = Impact.High, Urgency = Urgency.High, Priority = Priority.Planning };
        var context = await RunAsync(change, RecordChangeType.Created);

        Assert.False(context.IsAborted);
        Assert.Equal(Priority.Critical, change.Priority);
        Assert.Equal(_clock.UtcNow, change.OpenedAt);
        Assert.True(change.Active);
    }

    [Fact]
    public async Task SkippingStates_IsAborted()
    {
        var change = Planned(new ChangeRequest { State = ChangeState.Scheduled });
        var context = await RunAsync(change, RecordChangeType.Updated, new RecordFieldChange(nameof(ChangeRequest.State), ChangeState.New, ChangeState.Scheduled));

        Assert.True(context.IsAborted);
        Assert.Contains("cannot move from New to Scheduled", context.AbortMessage);
    }

    [Fact]
    public async Task Scheduling_RequiresApprovedCab()
    {
        var change = Planned(new ChangeRequest { State = ChangeState.Scheduled, Approval = ApprovalStatus.Requested });
        var transition = new RecordFieldChange(nameof(ChangeRequest.State), ChangeState.Authorize, ChangeState.Scheduled);
        Assert.True((await RunAsync(change, RecordChangeType.Updated, transition)).IsAborted);

        change.Approval = ApprovalStatus.Approved;
        Assert.False((await RunAsync(change, RecordChangeType.Updated, transition)).IsAborted);
    }

    [Fact]
    public async Task OldValueAsText_IsUnderstood()
    {
        var change = Planned(new ChangeRequest { State = ChangeState.Implement });
        var context = await RunAsync(change, RecordChangeType.Updated, new RecordFieldChange(nameof(ChangeRequest.State), "New", ChangeState.Implement));

        Assert.True(context.IsAborted);
    }

    [Fact]
    public async Task Closing_RequiresCloseCode_AndMaintainsClosure()
    {
        var change = Planned(new ChangeRequest { State = ChangeState.Closed });
        var transition = new RecordFieldChange(nameof(ChangeRequest.State), ChangeState.Review, ChangeState.Closed);
        Assert.True((await RunAsync(change, RecordChangeType.Updated, transition)).IsAborted);

        change.CloseCode = ChangeCloseCode.Successful;
        change.CloseNotes = "Implemented as planned";
        var context = await RunAsync(change, RecordChangeType.Updated, transition);

        Assert.False(context.IsAborted);
        Assert.False(change.Active);
        Assert.Equal(_clock.UtcNow, change.ClosedAt);
    }

    [Fact]
    public async Task UnchangedState_IsNotValidated()
    {
        var change = new ChangeRequest { State = ChangeState.Implement, ShortDescription = "Edited" };
        var context = await RunAsync(change, RecordChangeType.Updated, new RecordFieldChange(nameof(ChangeRequest.ShortDescription), "Old", "Edited"));

        Assert.False(context.IsAborted);
        Assert.True(change.Active);
    }

    [Fact]
    public async Task ChangeTask_ActiveFollowsState()
    {
        var task = new ChangeTask { State = ChangeTaskState.Completed, Impact = Impact.Medium, Urgency = Urgency.Medium };
        await RunAsync(task, RecordChangeType.Updated, new RecordFieldChange(nameof(ChangeTask.State), ChangeTaskState.InProgress, ChangeTaskState.Completed));

        Assert.False(task.Active);
        Assert.NotNull(task.ClosedAt);

        task.State = ChangeTaskState.InProgress;
        await RunAsync(task, RecordChangeType.Updated, new RecordFieldChange(nameof(ChangeTask.State), ChangeTaskState.Completed, ChangeTaskState.InProgress));
        Assert.True(task.Active);
        Assert.Null(task.ClosedAt);
    }

    private static ChangeRequest Planned(ChangeRequest change)
    {
        change.PlannedStartAt = new DateTimeOffset(2026, 11, 10, 20, 0, 0, TimeSpan.Zero);
        change.PlannedEndAt = change.PlannedStartAt.Value.AddHours(2);
        change.ImplementationPlan = "Plan";
        change.BackoutPlan = "Backout";
        change.RiskAssessedAt = change.PlannedStartAt.Value.AddDays(-2);
        return change;
    }

    private async Task<RecordSavingContext> RunAsync(object entity, RecordChangeType type, params RecordFieldChange[] changes)
    {
        var tableName = entity is ChangeTask ? ChangeTables.ChangeTask : Servora.Domain.Common.TableNames.ChangeRequest;
        var context = new RecordSavingContext(tableName, entity, type, changes);
        await new ChangeSavingHook(_clock).OnSavingAsync(context, CancellationToken.None);
        return context;
    }
}
