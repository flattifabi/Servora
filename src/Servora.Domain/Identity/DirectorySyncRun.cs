using Servora.Domain.Common;

namespace Servora.Domain.Identity;

/// <summary>History entry of a directory synchronisation run (scheduled or started with "Sync now").</summary>
public class DirectorySyncRun : Entity
{
    /// <summary>Gets or sets the UTC start time.</summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>Gets or sets the UTC end time.</summary>
    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary>Gets or sets what started the run.</summary>
    public DirectorySyncTrigger Trigger { get; set; }

    /// <summary>Gets or sets the outcome.</summary>
    public DirectorySyncStatus Status { get; set; } = DirectorySyncStatus.Running;

    /// <summary>Gets or sets the display name of the actor that started the run.</summary>
    public string? StartedBy { get; set; }

    /// <summary>Gets or sets the number of directory entries read.</summary>
    public int UsersRead { get; set; }

    /// <summary>Gets or sets the number of local users created.</summary>
    public int UsersCreated { get; set; }

    /// <summary>Gets or sets the number of local users updated.</summary>
    public int UsersUpdated { get; set; }

    /// <summary>Gets or sets the number of local users deactivated (disabled or missing in the directory).</summary>
    public int UsersDeactivated { get; set; }

    /// <summary>Gets or sets the number of role / group membership changes applied from group mappings.</summary>
    public int MembershipChanges { get; set; }

    /// <summary>Gets or sets the number of entries that failed to synchronise.</summary>
    public int Errors { get; set; }

    /// <summary>Gets or sets a user-safe summary or error message (never contains credentials).</summary>
    public string? Message { get; set; }
}
