namespace Servora.Domain.Changes;

/// <summary>State of a change task.</summary>
public enum ChangeTaskState
{
    /// <summary>Not started.</summary>
    Open = 0,

    /// <summary>Work in progress.</summary>
    InProgress = 1,

    /// <summary>Completed (final).</summary>
    Completed = 2,

    /// <summary>Cancelled (final).</summary>
    Cancelled = 3,
}
