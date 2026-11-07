namespace Servora.Application.Itsm;

/// <summary>Personalised workspace data of the current user (the "My Work" home page).</summary>
public sealed class MyWorkDashboard
{
    /// <summary>Gets a value indicating whether the user can read incidents.</summary>
    public bool CanReadIncidents { get; init; }

    /// <summary>Gets a value indicating whether the user can read problems.</summary>
    public bool CanReadProblems { get; init; }

    /// <summary>Gets the number of open incidents and problems assigned to the user.</summary>
    public int AssignedToMe { get; init; }

    /// <summary>Gets the number of assigned items with priority P1 or P2.</summary>
    public int AssignedHighPriority { get; init; }

    /// <summary>Gets the number of open, unassigned incidents and problems of the user's groups.</summary>
    public int GroupUnassigned { get; init; }

    /// <summary>Gets the number of groups the user belongs to.</summary>
    public int GroupCount { get; init; }

    /// <summary>Gets the number of open P1 incidents (all groups).</summary>
    public int CriticalOpen { get; init; }

    /// <summary>Gets the number of open major incidents.</summary>
    public int MajorOpen { get; init; }

    /// <summary>
    /// Gets the number of pending approvals of the user, or <see langword="null"/> when the approvals module cannot
    /// provide a per-user count.
    /// </summary>
    public int? PendingApprovals { get; init; }

    /// <summary>Gets the number of incidents the user resolved during the last 7 days.</summary>
    public int ResolvedByMeThisWeek { get; init; }

    /// <summary>Gets the user's open work items ordered by priority and last update.</summary>
    public IReadOnlyList<MyWorkItem> Items { get; init; } = [];

    /// <summary>Gets the recent activity on the user's records, newest first.</summary>
    public IReadOnlyList<MyWorkActivityItem> RecentActivity { get; init; } = [];

    /// <summary>Gets the day labels of the trend chart (oldest first).</summary>
    public IReadOnlyList<string> TrendLabels { get; init; } = [];

    /// <summary>Gets the incidents opened per day.</summary>
    public IReadOnlyList<double> OpenedPerDay { get; init; } = [];

    /// <summary>Gets the incidents resolved per day.</summary>
    public IReadOnlyList<double> ResolvedPerDay { get; init; } = [];

    /// <summary>Gets the open incident count per priority (P1..P5).</summary>
    public IReadOnlyList<int> OpenByPriority { get; init; } = [];
}
