namespace Servora.Domain.Identity;

/// <summary>Purpose of a <see cref="Group"/>.</summary>
public enum GroupType
{
    /// <summary>Team that work can be assigned to (service desk, network team, ...).</summary>
    Assignment = 0,

    /// <summary>Group that is only used to grant roles / permissions.</summary>
    Security = 1,

    /// <summary>Group that approves changes and requests (for example a change advisory board).</summary>
    Approval = 2,
}
