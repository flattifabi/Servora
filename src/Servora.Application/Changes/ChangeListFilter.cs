namespace Servora.Application.Changes;

/// <summary>Predefined filters of the change list.</summary>
public enum ChangeListFilter
{
    /// <summary>All active (not closed / cancelled) changes.</summary>
    Open = 0,

    /// <summary>Active changes assigned to the current user.</summary>
    AssignedToMe = 1,

    /// <summary>Changes waiting for CAB authorization.</summary>
    AwaitingApproval = 2,

    /// <summary>Scheduled changes and changes in implementation.</summary>
    Scheduled = 3,

    /// <summary>Active emergency changes.</summary>
    Emergency = 4,

    /// <summary>Closed and cancelled changes.</summary>
    Closed = 5,

    /// <summary>Every change.</summary>
    All = 6,
}
