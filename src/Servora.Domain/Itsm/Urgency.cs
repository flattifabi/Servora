namespace Servora.Domain.Itsm;

/// <summary>Time sensitivity of a task record.</summary>
public enum Urgency
{
    /// <summary>Work cannot continue.</summary>
    High = 1,

    /// <summary>Work is degraded.</summary>
    Medium = 2,

    /// <summary>Work can continue normally.</summary>
    Low = 3,
}
