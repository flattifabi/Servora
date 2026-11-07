namespace Servora.Domain.Itsm;

/// <summary>Lifecycle states of a <see cref="Problem"/>.</summary>
public enum ProblemState
{
    /// <summary>Logged but not yet assessed.</summary>
    New = 1,

    /// <summary>Impact and priority are being assessed.</summary>
    Assess = 2,

    /// <summary>The root cause is being investigated.</summary>
    RootCauseAnalysis = 3,

    /// <summary>The root cause is known and a permanent fix is being implemented.</summary>
    FixInProgress = 4,

    /// <summary>The fix was applied (requires a resolution code and fix notes).</summary>
    Resolved = 6,

    /// <summary>Final state: the problem was closed.</summary>
    Closed = 7,

    /// <summary>Final state: the problem was cancelled (for example a duplicate).</summary>
    Cancelled = 8,
}
