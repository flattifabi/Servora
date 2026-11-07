namespace Servora.Domain.Itsm;

/// <summary>
/// Allowed state transitions and state-dependent mandatory fields of <see cref="Problem"/>s.
/// </summary>
/// <remarks>
/// <code>
/// New ──► Assess ──► Root Cause Analysis ◄──► Fix in Progress ──► Resolved ──► Closed
///  └─────────────────────────┘        └──────────────────────────────►┘   └──► Fix in Progress (reopen)
/// any active state before Resolved ──► Cancelled
/// </code>
/// </remarks>
public static class ProblemStateMachine
{
    private static readonly IReadOnlyDictionary<ProblemState, ProblemState[]> s_transitions = new Dictionary<ProblemState, ProblemState[]>
    {
        [ProblemState.New] = [ProblemState.Assess, ProblemState.RootCauseAnalysis, ProblemState.Cancelled],
        [ProblemState.Assess] = [ProblemState.RootCauseAnalysis, ProblemState.Cancelled],
        [ProblemState.RootCauseAnalysis] = [ProblemState.FixInProgress, ProblemState.Resolved, ProblemState.Assess, ProblemState.Cancelled],
        [ProblemState.FixInProgress] = [ProblemState.Resolved, ProblemState.RootCauseAnalysis, ProblemState.Cancelled],
        [ProblemState.Resolved] = [ProblemState.Closed, ProblemState.FixInProgress],
        [ProblemState.Closed] = [],
        [ProblemState.Cancelled] = [],
    };

    /// <summary>Gets the ordered main path used for progress displays.</summary>
    public static IReadOnlyList<ProblemState> MainPath { get; } =
        [ProblemState.New, ProblemState.Assess, ProblemState.RootCauseAnalysis, ProblemState.FixInProgress, ProblemState.Resolved, ProblemState.Closed];

    /// <summary>Gets the states that can follow the given state.</summary>
    /// <param name="from">The current state.</param>
    /// <returns>The allowed target states.</returns>
    public static IReadOnlyList<ProblemState> AllowedTargets(ProblemState from) =>
        s_transitions.TryGetValue(from, out var targets) ? targets : [];

    /// <summary>Checks whether a transition is allowed. Staying in the same state is always allowed.</summary>
    /// <param name="from">The current state.</param>
    /// <param name="to">The target state.</param>
    /// <returns><see langword="true"/> when allowed.</returns>
    public static bool CanTransition(ProblemState from, ProblemState to) => from == to || AllowedTargets(from).Contains(to);

    /// <summary>Checks whether the state is final (closed or cancelled).</summary>
    /// <param name="state">The state.</param>
    /// <returns><see langword="true"/> for final states.</returns>
    public static bool IsFinal(ProblemState state) => state is ProblemState.Closed or ProblemState.Cancelled;

    /// <summary>Checks whether the record is active (not closed or cancelled).</summary>
    /// <param name="state">The state.</param>
    /// <returns><see langword="true"/> when active.</returns>
    public static bool IsActive(ProblemState state) => !IsFinal(state);

    /// <summary>Checks whether investigation or fixing is still outstanding.</summary>
    /// <param name="state">The state.</param>
    /// <returns><see langword="true"/> when open.</returns>
    public static bool IsOpen(ProblemState state) => state is ProblemState.New or ProblemState.Assess or ProblemState.RootCauseAnalysis or ProblemState.FixInProgress;

    /// <summary>Validates the mandatory fields of the problem's current state.</summary>
    /// <param name="problem">The problem.</param>
    /// <returns>A user-safe error message, or <see langword="null"/> when valid.</returns>
    public static string? ValidateStateFields(Problem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        if (problem.State == ProblemState.FixInProgress && string.IsNullOrWhiteSpace(problem.RootCause))
        {
            return "Document the root cause before starting the fix.";
        }

        if (problem.State is ProblemState.Resolved or ProblemState.Closed)
        {
            if (problem.ResolutionCode is null)
            {
                return "A resolution code is required to resolve a problem.";
            }

            if (string.IsNullOrWhiteSpace(problem.FixNotes))
            {
                return "Fix notes are required to resolve a problem.";
            }
        }

        if (problem.IsKnownError && string.IsNullOrWhiteSpace(problem.Workaround))
        {
            return "A known error must document a workaround.";
        }

        return null;
    }

    /// <summary>Validates a transition including the mandatory fields of the target state.</summary>
    /// <param name="from">The previous state.</param>
    /// <param name="problem">The problem with its new state and field values.</param>
    /// <returns>A user-safe error message, or <see langword="null"/> when valid.</returns>
    public static string? ValidateTransition(ProblemState from, Problem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        if (!CanTransition(from, problem.State))
        {
            return $"A problem cannot move from {ItsmLabels.For(from)} to {ItsmLabels.For(problem.State)}.";
        }

        return ValidateStateFields(problem);
    }
}
