using Servora.Application.Approvals;
using Servora.Domain.Approvals;

namespace Servora.Tests.Unit.ServiceManagement;

/// <summary>Tests for <see cref="ApprovalPolicyEvaluator"/>.</summary>
public sealed class ApprovalPolicyEvaluatorTests
{
    private const ApprovalRecordState R = ApprovalRecordState.Requested;
    private const ApprovalRecordState A = ApprovalRecordState.Approved;
    private const ApprovalRecordState X = ApprovalRecordState.Rejected;
    private const ApprovalRecordState C = ApprovalRecordState.Cancelled;
    private const ApprovalRecordState N = ApprovalRecordState.NotRequired;

    [Fact]
    public void NoRows_IsNotRequested()
    {
        Assert.Equal(ApprovalStatus.NotRequested, ApprovalPolicyEvaluator.Evaluate(ApprovalMode.AnyApprover, []));
        Assert.Equal(ApprovalStatus.NotRequested, ApprovalPolicyEvaluator.Evaluate(ApprovalMode.AllApprovers, []));
    }

    [Theory]
    [InlineData(new[] { R, R, R }, ApprovalStatus.Requested)]
    [InlineData(new[] { A, R, R }, ApprovalStatus.Approved)]
    [InlineData(new[] { A, N, N }, ApprovalStatus.Approved)]
    [InlineData(new[] { X, R }, ApprovalStatus.Rejected)]
    [InlineData(new[] { X, N }, ApprovalStatus.Rejected)]
    [InlineData(new[] { C, C }, ApprovalStatus.Cancelled)]
    public void AnyApprover_FirstDecisionWins(ApprovalRecordState[] states, ApprovalStatus expected) =>
        Assert.Equal(expected, ApprovalPolicyEvaluator.Evaluate(ApprovalMode.AnyApprover, states));

    [Theory]
    [InlineData(new[] { R, R }, ApprovalStatus.Requested)]
    [InlineData(new[] { A, R }, ApprovalStatus.Requested)]
    [InlineData(new[] { A, A, A }, ApprovalStatus.Approved)]
    [InlineData(new[] { A, X, R }, ApprovalStatus.Rejected)]
    [InlineData(new[] { X, N, N }, ApprovalStatus.Rejected)]
    [InlineData(new[] { A, C }, ApprovalStatus.Cancelled)]
    public void AllApprovers_RequiresEveryApproval(ApprovalRecordState[] states, ApprovalStatus expected) =>
        Assert.Equal(expected, ApprovalPolicyEvaluator.Evaluate(ApprovalMode.AllApprovers, states));

    [Fact]
    public void ContractMapping_IsConsistent()
    {
        Assert.Equal(ApprovalState.Approved, ApprovalPolicyEvaluator.ToContract(ApprovalStatus.Approved));
        Assert.Equal(ApprovalState.Rejected, ApprovalPolicyEvaluator.ToContract(ApprovalStatus.Rejected));
        Assert.Equal(ApprovalMode.AllApprovers, ApprovalPolicyEvaluator.ToMode(ApprovalPolicy.AllApprovers));
        Assert.True(ApprovalPolicyEvaluator.IsDecided(ApprovalStatus.Rejected));
        Assert.False(ApprovalPolicyEvaluator.IsDecided(ApprovalStatus.Requested));
    }
}
