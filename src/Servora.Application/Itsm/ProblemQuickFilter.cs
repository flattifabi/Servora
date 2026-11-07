namespace Servora.Application.Itsm;

/// <summary>Saved quick filters of the problem list.</summary>
public enum ProblemQuickFilter
{
    /// <summary>All active problems (not closed or cancelled).</summary>
    AllActive = 0,

    /// <summary>Open problems assigned to the current user.</summary>
    MyOpen = 1,

    /// <summary>Open problems without an assignee.</summary>
    Unassigned = 2,

    /// <summary>Open problems with priority P1 or P2.</summary>
    HighPriority = 3,

    /// <summary>Known errors (documented workaround).</summary>
    KnownErrors = 4,

    /// <summary>Resolved problems awaiting closure.</summary>
    Resolved = 5,

    /// <summary>Every problem including closed and cancelled ones.</summary>
    All = 6,
}
