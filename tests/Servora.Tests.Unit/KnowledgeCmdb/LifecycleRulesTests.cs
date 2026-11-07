using Servora.Domain.Assets;
using Servora.Domain.Knowledge;

namespace Servora.Tests.Unit.KnowledgeCmdb;

/// <summary>Tests for the pure knowledge workflow and asset lifecycle rules.</summary>
public sealed class LifecycleRulesTests
{
    [Theory]
    [InlineData(KnowledgeArticleState.Draft, KnowledgeWorkflowAction.SubmitForReview, KnowledgeArticleState.Review)]
    [InlineData(KnowledgeArticleState.Review, KnowledgeWorkflowAction.Publish, KnowledgeArticleState.Published)]
    [InlineData(KnowledgeArticleState.Review, KnowledgeWorkflowAction.Reject, KnowledgeArticleState.Draft)]
    [InlineData(KnowledgeArticleState.Published, KnowledgeWorkflowAction.Retire, KnowledgeArticleState.Retired)]
    [InlineData(KnowledgeArticleState.Retired, KnowledgeWorkflowAction.Revise, KnowledgeArticleState.Draft)]
    public void Workflow_AllowedTransitions(KnowledgeArticleState from, KnowledgeWorkflowAction action, KnowledgeArticleState to)
    {
        Assert.Equal(to, KnowledgeWorkflow.TargetOf(from, action));
    }

    [Theory]
    [InlineData(KnowledgeArticleState.Draft, KnowledgeWorkflowAction.Retire)]
    [InlineData(KnowledgeArticleState.Published, KnowledgeWorkflowAction.SubmitForReview)]
    [InlineData(KnowledgeArticleState.Retired, KnowledgeWorkflowAction.Publish)]
    [InlineData(KnowledgeArticleState.Published, KnowledgeWorkflowAction.Reject)]
    public void Workflow_RejectsInvalidTransitions(KnowledgeArticleState from, KnowledgeWorkflowAction action)
    {
        Assert.Null(KnowledgeWorkflow.TargetOf(from, action));
    }

    [Fact]
    public void Workflow_OnlyDraftsAndReviewsAreEditable()
    {
        Assert.True(KnowledgeWorkflow.IsEditable(KnowledgeArticleState.Draft));
        Assert.True(KnowledgeWorkflow.IsEditable(KnowledgeArticleState.Review));
        Assert.False(KnowledgeWorkflow.IsEditable(KnowledgeArticleState.Published));
        Assert.False(KnowledgeWorkflow.IsEditable(KnowledgeArticleState.Retired));
        Assert.True(KnowledgeWorkflow.RequiresReviewer(KnowledgeWorkflowAction.Publish));
        Assert.False(KnowledgeWorkflow.RequiresReviewer(KnowledgeWorkflowAction.SubmitForReview));
    }

    [Theory]
    [InlineData(AssetState.InStock, AssetState.InUse, true)]
    [InlineData(AssetState.InUse, AssetState.InRepair, true)]
    [InlineData(AssetState.InRepair, AssetState.InStock, true)]
    [InlineData(AssetState.InUse, AssetState.Retired, true)]
    [InlineData(AssetState.Retired, AssetState.Disposed, true)]
    [InlineData(AssetState.InUse, AssetState.Disposed, false)]
    [InlineData(AssetState.Disposed, AssetState.InStock, false)]
    [InlineData(AssetState.InStock, AssetState.InStock, false)]
    public void AssetLifecycle_Transitions(AssetState from, AssetState to, bool allowed)
    {
        Assert.Equal(allowed, AssetLifecycle.CanTransition(from, to));
    }
}
