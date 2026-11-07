namespace Servora.Domain.Itsm;

/// <summary>
/// Allowed state transitions and state-dependent mandatory fields of <see cref="Incident"/>s.
/// </summary>
/// <remarks>
/// <code>
/// New ──► In Progress ◄──► On Hold
///  │          │               │
///  └──────────┴──► Resolved ◄─┘ ──► Closed
///                     │
///                     └──► In Progress (reopen)
/// any open state ──► Cancelled
/// </code>
/// </remarks>
public static class IncidentStateMachine
{
    private static readonly IReadOnlyDictionary<IncidentState, IncidentState[]> s_transitions = new Dictionary<IncidentState, IncidentState[]>
    {
        [IncidentState.New] = [IncidentState.InProgress, IncidentState.OnHold, IncidentState.Resolved, IncidentState.Cancelled],
        [IncidentState.InProgress] = [IncidentState.OnHold, IncidentState.Resolved, IncidentState.Cancelled],
        [IncidentState.OnHold] = [IncidentState.InProgress, IncidentState.Resolved, IncidentState.Cancelled],
        [IncidentState.Resolved] = [IncidentState.Closed, IncidentState.InProgress],
        [IncidentState.Closed] = [],
        [IncidentState.Cancelled] = [],
    };

    /// <summary>Gets the states that can follow the given state.</summary>
    /// <param name="from">The current state.</param>
    /// <returns>The allowed target states.</returns>
    public static IReadOnlyList<IncidentState> AllowedTargets(IncidentState from) =>
        s_transitions.TryGetValue(from, out var targets) ? targets : [];

    /// <summary>Checks whether a transition is allowed. Staying in the same state is always allowed.</summary>
    /// <param name="from">The current state.</param>
    /// <param name="to">The target state.</param>
    /// <returns><see langword="true"/> when allowed.</returns>
    public static bool CanTransition(IncidentState from, IncidentState to) => from == to || AllowedTargets(from).Contains(to);

    /// <summary>Checks whether the state is final (closed or cancelled).</summary>
    /// <param name="state">The state.</param>
    /// <returns><see langword="true"/> for final states.</returns>
    public static bool IsFinal(IncidentState state) => state is IncidentState.Closed or IncidentState.Cancelled;

    /// <summary>
    /// Checks whether the record is still active (not closed or cancelled). Resolved incidents stay active until they
    /// are closed, so they can still be reopened.
    /// </summary>
    /// <param name="state">The state.</param>
    /// <returns><see langword="true"/> when active.</returns>
    public static bool IsActive(IncidentState state) => !IsFinal(state);

    /// <summary>Checks whether work is still outstanding (new, in progress or on hold).</summary>
    /// <param name="state">The state.</param>
    /// <returns><see langword="true"/> when open.</returns>
    public static bool IsOpen(IncidentState state) => state is IncidentState.New or IncidentState.InProgress or IncidentState.OnHold;

    /// <summary>Validates the mandatory fields of the incident's current state.</summary>
    /// <param name="incident">The incident.</param>
    /// <returns>A user-safe error message, or <see langword="null"/> when the incident is valid.</returns>
    public static string? ValidateStateFields(Incident incident)
    {
        ArgumentNullException.ThrowIfNull(incident);
        if (incident.State == IncidentState.OnHold && incident.OnHoldReason is null)
        {
            return "An on-hold reason is required to put an incident on hold.";
        }

        if (incident.State is IncidentState.Resolved or IncidentState.Closed)
        {
            if (incident.ResolutionCode is null)
            {
                return "A resolution code is required to resolve an incident.";
            }

            if (string.IsNullOrWhiteSpace(incident.ResolutionNotes))
            {
                return "Resolution notes are required to resolve an incident.";
            }
        }

        return null;
    }

    /// <summary>Validates a transition including the mandatory fields of the target state.</summary>
    /// <param name="from">The previous state.</param>
    /// <param name="incident">The incident with its new state and field values.</param>
    /// <returns>A user-safe error message, or <see langword="null"/> when valid.</returns>
    public static string? ValidateTransition(IncidentState from, Incident incident)
    {
        ArgumentNullException.ThrowIfNull(incident);
        if (!CanTransition(from, incident.State))
        {
            return $"An incident cannot move from {ItsmLabels.For(from)} to {ItsmLabels.For(incident.State)}.";
        }

        return ValidateStateFields(incident);
    }
}
