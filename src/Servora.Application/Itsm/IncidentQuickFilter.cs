namespace Servora.Application.Itsm;

/// <summary>Saved quick filters of the incident list.</summary>
public enum IncidentQuickFilter
{
    /// <summary>All active incidents (not closed or cancelled).</summary>
    AllActive = 0,

    /// <summary>Open incidents assigned to the current user.</summary>
    MyOpen = 1,

    /// <summary>Open incidents without an assignee.</summary>
    Unassigned = 2,

    /// <summary>Open incidents with priority P1 or P2.</summary>
    HighPriority = 3,

    /// <summary>Open incidents of the current user's groups.</summary>
    MyGroups = 4,

    /// <summary>Open major incidents.</summary>
    Major = 5,

    /// <summary>Resolved incidents awaiting closure.</summary>
    Resolved = 6,

    /// <summary>Every incident including closed and cancelled ones.</summary>
    All = 7,
}
