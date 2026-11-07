namespace Servora.Application.Catalog;

/// <summary>Predefined filters of the fulfiller request lists.</summary>
public enum RequestListFilter
{
    /// <summary>Active records.</summary>
    Open = 0,

    /// <summary>Active records assigned to one of my groups (items and tasks).</summary>
    MyGroups = 1,

    /// <summary>Active records assigned to me (tasks).</summary>
    AssignedToMe = 2,

    /// <summary>Records waiting for approval (requests and items).</summary>
    AwaitingApproval = 3,

    /// <summary>Closed records.</summary>
    Closed = 4,

    /// <summary>Every record.</summary>
    All = 5,
}
