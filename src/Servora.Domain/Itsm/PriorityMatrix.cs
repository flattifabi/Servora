namespace Servora.Domain.Itsm;

/// <summary>
/// Default ITIL impact × urgency priority matrix. Administrators can override the mapping through system
/// properties; this type provides the built-in default.
/// </summary>
public static class PriorityMatrix
{
    /// <summary>Calculates the priority for the given impact and urgency.</summary>
    /// <param name="impact">The business impact.</param>
    /// <param name="urgency">The urgency.</param>
    /// <returns>The resulting priority (P1 for high/high down to P5 for low/low).</returns>
    public static Priority Calculate(Impact impact, Urgency urgency) => ((int)impact + (int)urgency) switch
    {
        2 => Priority.Critical,
        3 => Priority.High,
        4 => Priority.Moderate,
        5 => Priority.Low,
        _ => Priority.Planning,
    };
}
