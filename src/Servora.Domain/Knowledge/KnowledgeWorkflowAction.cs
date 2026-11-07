namespace Servora.Domain.Knowledge;

/// <summary>A workflow action that moves a knowledge article between states.</summary>
public enum KnowledgeWorkflowAction
{
    /// <summary>Draft → Review.</summary>
    SubmitForReview = 1,

    /// <summary>Review → Published (or Draft → Published for reviewers).</summary>
    Publish = 2,

    /// <summary>Review → Draft.</summary>
    Reject = 3,

    /// <summary>Published → Retired.</summary>
    Retire = 4,

    /// <summary>Published or Retired → Draft (start a new revision).</summary>
    Revise = 5,
}
