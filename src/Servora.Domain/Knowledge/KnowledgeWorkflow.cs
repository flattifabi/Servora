namespace Servora.Domain.Knowledge;

/// <summary>Pure rules of the knowledge article workflow (Draft → Review → Published → Retired).</summary>
public static class KnowledgeWorkflow
{
    /// <summary>Gets the ordered states for state flow visualisations.</summary>
    public static IReadOnlyList<KnowledgeArticleState> OrderedStates { get; } =
        [KnowledgeArticleState.Draft, KnowledgeArticleState.Review, KnowledgeArticleState.Published, KnowledgeArticleState.Retired];

    /// <summary>Gets the target state of an action when it is allowed from the given state.</summary>
    /// <param name="from">The current state.</param>
    /// <param name="action">The action.</param>
    /// <returns>The target state, or <see langword="null"/> when the action is not allowed in the current state.</returns>
    public static KnowledgeArticleState? TargetOf(KnowledgeArticleState from, KnowledgeWorkflowAction action) => (from, action) switch
    {
        (KnowledgeArticleState.Draft, KnowledgeWorkflowAction.SubmitForReview) => KnowledgeArticleState.Review,
        (KnowledgeArticleState.Draft, KnowledgeWorkflowAction.Publish) => KnowledgeArticleState.Published,
        (KnowledgeArticleState.Review, KnowledgeWorkflowAction.Publish) => KnowledgeArticleState.Published,
        (KnowledgeArticleState.Review, KnowledgeWorkflowAction.Reject) => KnowledgeArticleState.Draft,
        (KnowledgeArticleState.Published, KnowledgeWorkflowAction.Retire) => KnowledgeArticleState.Retired,
        (KnowledgeArticleState.Published, KnowledgeWorkflowAction.Revise) => KnowledgeArticleState.Draft,
        (KnowledgeArticleState.Retired, KnowledgeWorkflowAction.Revise) => KnowledgeArticleState.Draft,
        _ => null,
    };

    /// <summary>Gets the actions that are allowed from a state.</summary>
    /// <param name="from">The current state.</param>
    /// <returns>The allowed actions.</returns>
    public static IReadOnlyList<KnowledgeWorkflowAction> AllowedActions(KnowledgeArticleState from) =>
        Enum.GetValues<KnowledgeWorkflowAction>().Where(a => TargetOf(from, a) is not null).ToList();

    /// <summary>Gets a value indicating whether an action requires the publish permission (reviewer) instead of the write permission.</summary>
    /// <param name="action">The action.</param>
    /// <returns><see langword="true"/> for reviewer actions.</returns>
    public static bool RequiresReviewer(KnowledgeWorkflowAction action) =>
        action is KnowledgeWorkflowAction.Publish or KnowledgeWorkflowAction.Reject or KnowledgeWorkflowAction.Retire;

    /// <summary>Gets a value indicating whether the content of an article in the given state may be edited.</summary>
    /// <param name="state">The state.</param>
    /// <returns><see langword="true"/> for drafts and articles in review.</returns>
    public static bool IsEditable(KnowledgeArticleState state) => state is KnowledgeArticleState.Draft or KnowledgeArticleState.Review;

    /// <summary>Gets the display label of a state.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The label.</returns>
    public static string Label(KnowledgeArticleState state) => state switch
    {
        KnowledgeArticleState.Draft => "Draft",
        KnowledgeArticleState.Review => "Review",
        KnowledgeArticleState.Published => "Published",
        KnowledgeArticleState.Retired => "Retired",
        _ => state.ToString(),
    };

    /// <summary>Gets the display label of an action.</summary>
    /// <param name="action">The action.</param>
    /// <returns>The label.</returns>
    public static string Label(KnowledgeWorkflowAction action) => action switch
    {
        KnowledgeWorkflowAction.SubmitForReview => "Submit for review",
        KnowledgeWorkflowAction.Publish => "Publish",
        KnowledgeWorkflowAction.Reject => "Send back to draft",
        KnowledgeWorkflowAction.Retire => "Retire",
        KnowledgeWorkflowAction.Revise => "Create new revision",
        _ => action.ToString(),
    };
}
