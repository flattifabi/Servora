namespace Servora.Domain.Itsm;

/// <summary>Human readable English labels of the ITSM enumerations (used by forms, lists and activity entries).</summary>
public static class ItsmLabels
{
    /// <summary>Gets the label of an incident state.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The label.</returns>
    public static string For(IncidentState state) => state switch
    {
        IncidentState.New => "New",
        IncidentState.InProgress => "In Progress",
        IncidentState.OnHold => "On Hold",
        IncidentState.Resolved => "Resolved",
        IncidentState.Closed => "Closed",
        IncidentState.Cancelled => "Cancelled",
        _ => state.ToString(),
    };

    /// <summary>Gets the label of a problem state.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The label.</returns>
    public static string For(ProblemState state) => state switch
    {
        ProblemState.New => "New",
        ProblemState.Assess => "Assess",
        ProblemState.RootCauseAnalysis => "Root Cause Analysis",
        ProblemState.FixInProgress => "Fix in Progress",
        ProblemState.Resolved => "Resolved",
        ProblemState.Closed => "Closed",
        ProblemState.Cancelled => "Cancelled",
        _ => state.ToString(),
    };

    /// <summary>Gets the label of an on-hold reason.</summary>
    /// <param name="reason">The reason.</param>
    /// <returns>The label.</returns>
    public static string For(OnHoldReason reason) => reason switch
    {
        OnHoldReason.AwaitingCaller => "Awaiting caller",
        OnHoldReason.AwaitingChange => "Awaiting change",
        OnHoldReason.AwaitingProblem => "Awaiting problem",
        OnHoldReason.AwaitingVendor => "Awaiting vendor",
        _ => reason.ToString(),
    };

    /// <summary>Gets the label of an incident resolution code.</summary>
    /// <param name="code">The code.</param>
    /// <returns>The label.</returns>
    public static string For(IncidentResolutionCode code) => code switch
    {
        IncidentResolutionCode.SolvedPermanently => "Solved (permanently)",
        IncidentResolutionCode.SolvedWorkaround => "Solved (workaround)",
        IncidentResolutionCode.SolvedRemotely => "Solved remotely",
        IncidentResolutionCode.NotReproducible => "Not reproducible",
        IncidentResolutionCode.NotSolvedTooCostly => "Not solved (too costly)",
        IncidentResolutionCode.ClosedByCaller => "Closed by caller",
        IncidentResolutionCode.Duplicate => "Duplicate",
        _ => code.ToString(),
    };

    /// <summary>Gets the label of a problem resolution code.</summary>
    /// <param name="code">The code.</param>
    /// <returns>The label.</returns>
    public static string For(ProblemResolutionCode code) => code switch
    {
        ProblemResolutionCode.FixApplied => "Fix applied",
        ProblemResolutionCode.RiskAccepted => "Risk accepted",
        ProblemResolutionCode.Duplicate => "Duplicate",
        _ => code.ToString(),
    };

    /// <summary>Gets the label of a contact channel.</summary>
    /// <param name="channel">The channel.</param>
    /// <returns>The label.</returns>
    public static string For(ContactChannel channel) => channel switch
    {
        ContactChannel.Portal => "Self-service portal",
        ContactChannel.Phone => "Phone",
        ContactChannel.Email => "E-mail",
        ContactChannel.Monitoring => "Monitoring",
        ContactChannel.Chat => "Chat",
        _ => channel.ToString(),
    };

    /// <summary>Gets the label of an impact.</summary>
    /// <param name="impact">The impact.</param>
    /// <returns>The label.</returns>
    public static string For(Impact impact) => impact switch
    {
        Impact.High => "1 - High",
        Impact.Medium => "2 - Medium",
        Impact.Low => "3 - Low",
        _ => impact.ToString(),
    };

    /// <summary>Gets the label of an urgency.</summary>
    /// <param name="urgency">The urgency.</param>
    /// <returns>The label.</returns>
    public static string For(Urgency urgency) => urgency switch
    {
        Urgency.High => "1 - High",
        Urgency.Medium => "2 - Medium",
        Urgency.Low => "3 - Low",
        _ => urgency.ToString(),
    };

    /// <summary>Gets the label of a priority.</summary>
    /// <param name="priority">The priority.</param>
    /// <returns>The label, for example <c>P1 - Critical</c>.</returns>
    public static string For(Priority priority) => priority switch
    {
        Priority.Critical => "P1 - Critical",
        Priority.High => "P2 - High",
        Priority.Moderate => "P3 - Moderate",
        Priority.Low => "P4 - Low",
        Priority.Planning => "P5 - Planning",
        _ => priority.ToString(),
    };
}
