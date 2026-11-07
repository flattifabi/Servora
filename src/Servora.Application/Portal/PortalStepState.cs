namespace Servora.Application.Portal;

/// <summary>State of one step of a portal status timeline.</summary>
public enum PortalStepState
{
    /// <summary>The step is done.</summary>
    Completed = 0,

    /// <summary>The item is currently in this step.</summary>
    Current = 1,

    /// <summary>The step has not been reached yet.</summary>
    Upcoming = 2,

    /// <summary>The item ended in this step without success (rejected or cancelled).</summary>
    Failed = 3,
}
