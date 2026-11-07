namespace Servora.Domain.Itsm;

/// <summary>Reason why an incident is <see cref="IncidentState.OnHold"/>.</summary>
public enum OnHoldReason
{
    /// <summary>Waiting for information or confirmation from the caller.</summary>
    AwaitingCaller = 1,

    /// <summary>Waiting for a change to be implemented.</summary>
    AwaitingChange = 2,

    /// <summary>Waiting for the related problem to be fixed.</summary>
    AwaitingProblem = 3,

    /// <summary>Waiting for an external vendor or supplier.</summary>
    AwaitingVendor = 4,
}
