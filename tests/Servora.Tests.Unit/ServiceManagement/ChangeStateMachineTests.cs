using Servora.Application.Changes;
using Servora.Domain.Approvals;
using Servora.Domain.Changes;

namespace Servora.Tests.Unit.ServiceManagement;

/// <summary>Tests for <see cref="ChangeStateMachine"/> and <see cref="ChangeRiskCalculator"/>.</summary>
public sealed class ChangeStateMachineTests
{
    private static readonly DateTimeOffset s_start = new(2026, 12, 1, 20, 0, 0, TimeSpan.Zero);

    private static ChangeTransitionContext Ready(ChangeType type, ChangeState current, ChangeState target) => new()
    {
        Type = type,
        Current = current,
        Target = target,
        PlannedStartAt = s_start,
        PlannedEndAt = s_start.AddHours(2),
        HasImplementationPlan = true,
        HasBackoutPlan = true,
        RiskAssessed = true,
        Approval = ApprovalStatus.NotRequested,
    };

    [Fact]
    public void NormalChange_FollowsFullLifecycle()
    {
        Assert.Equal([ChangeState.Assess, ChangeState.Cancelled], ChangeStateMachine.GetNextStates(ChangeType.Normal, ChangeState.New));
        Assert.Contains(ChangeState.Authorize, ChangeStateMachine.GetNextStates(ChangeType.Normal, ChangeState.Assess));
        Assert.Equal([ChangeState.Review], ChangeStateMachine.GetNextStates(ChangeType.Normal, ChangeState.Implement));
        Assert.Empty(ChangeStateMachine.GetNextStates(ChangeType.Normal, ChangeState.Closed));
        Assert.Equal(7, ChangeStateMachine.GetProgression(ChangeType.Normal).Count);
    }

    [Fact]
    public void StandardChange_SkipsAssessmentAndAuthorization()
    {
        Assert.Equal([ChangeState.Scheduled, ChangeState.Cancelled], ChangeStateMachine.GetNextStates(ChangeType.Standard, ChangeState.New));
        Assert.DoesNotContain(ChangeState.Authorize, ChangeStateMachine.GetProgression(ChangeType.Standard));
        Assert.Null(ChangeStateMachine.Validate(Ready(ChangeType.Standard, ChangeState.New, ChangeState.Scheduled) with { HasBackoutPlan = false }));
    }

    [Fact]
    public void SkippingStates_IsRejected()
    {
        Assert.NotNull(ChangeStateMachine.Validate(Ready(ChangeType.Normal, ChangeState.New, ChangeState.Implement)));
        Assert.NotNull(ChangeStateMachine.Validate(Ready(ChangeType.Normal, ChangeState.Implement, ChangeState.Cancelled)));
        Assert.NotNull(ChangeStateMachine.Validate(Ready(ChangeType.Normal, ChangeState.Closed, ChangeState.New)));
    }

    [Fact]
    public void Authorize_RequiresPlansWindowAndRisk()
    {
        Assert.Null(ChangeStateMachine.Validate(Ready(ChangeType.Normal, ChangeState.Assess, ChangeState.Authorize)));
        Assert.NotNull(ChangeStateMachine.Validate(Ready(ChangeType.Normal, ChangeState.Assess, ChangeState.Authorize) with { PlannedEndAt = s_start }));
        Assert.NotNull(ChangeStateMachine.Validate(Ready(ChangeType.Normal, ChangeState.Assess, ChangeState.Authorize) with { HasBackoutPlan = false }));
        Assert.NotNull(ChangeStateMachine.Validate(Ready(ChangeType.Normal, ChangeState.Assess, ChangeState.Authorize) with { RiskAssessed = false }));

        // Emergency changes may skip the questionnaire.
        Assert.Null(ChangeStateMachine.Validate(Ready(ChangeType.Emergency, ChangeState.Assess, ChangeState.Authorize) with { RiskAssessed = false }));
    }

    [Fact]
    public void Scheduling_RequiresApprovedCab()
    {
        var context = Ready(ChangeType.Normal, ChangeState.Authorize, ChangeState.Scheduled);
        Assert.NotNull(ChangeStateMachine.Validate(context with { Approval = ApprovalStatus.Requested }));
        Assert.NotNull(ChangeStateMachine.Validate(context with { Approval = ApprovalStatus.Rejected }));
        Assert.Null(ChangeStateMachine.Validate(context with { Approval = ApprovalStatus.Approved }));
    }

    [Fact]
    public void Closing_RequiresCloseCodeNotesAndClosedTasks()
    {
        var context = Ready(ChangeType.Normal, ChangeState.Review, ChangeState.Closed);
        Assert.NotNull(ChangeStateMachine.Validate(context));
        Assert.NotNull(ChangeStateMachine.Validate(context with { CloseCode = ChangeCloseCode.Successful, CloseNotes = "   " }));
        Assert.NotNull(ChangeStateMachine.Validate(context with { CloseCode = ChangeCloseCode.Successful, CloseNotes = "Done", OpenTaskCount = 1 }));
        Assert.Null(ChangeStateMachine.Validate(context with { CloseCode = ChangeCloseCode.Successful, CloseNotes = "Done" }));
    }

    [Fact]
    public void RiskCalculator_MapsScoresToLevels()
    {
        var all = ChangeRiskCalculator.Questions.ToDictionary(q => q.Key, _ => 0);
        Assert.Equal(new RiskAssessmentResult(0, ChangeRisk.Low), ChangeRiskCalculator.Calculate(all));

        var worst = ChangeRiskCalculator.Questions.ToDictionary(q => q.Key, q => q.Options.Count - 1);
        Assert.Equal(ChangeRisk.VeryHigh, ChangeRiskCalculator.Calculate(worst)!.Risk);

        all.Remove("outage");
        Assert.Null(ChangeRiskCalculator.Calculate(all));
        Assert.Null(ChangeRiskCalculator.Calculate(worst.ToDictionary(p => p.Key, p => p.Value + 5)));
        Assert.Equal(ChangeRisk.Moderate, ChangeRiskCalculator.RiskFor(4));
        Assert.Equal(ChangeRisk.High, ChangeRiskCalculator.RiskFor(11));
    }
}
