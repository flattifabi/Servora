namespace Servora.Domain.Knowledge;

/// <summary>Workflow state of a knowledge article.</summary>
public enum KnowledgeArticleState
{
    /// <summary>Being written; only visible to authors and reviewers.</summary>
    Draft = 1,

    /// <summary>Submitted for review.</summary>
    Review = 2,

    /// <summary>Published and visible to readers.</summary>
    Published = 3,

    /// <summary>Retired; no longer visible to readers.</summary>
    Retired = 4,
}
