using Servora.Domain.Identity;

namespace Servora.Application.Identity.Ldap;

/// <summary>Summary of a directory synchronisation run (also used as history row).</summary>
/// <param name="RunId">The run identifier (empty when the run could not start).</param>
/// <param name="Trigger">What started the run.</param>
/// <param name="Status">The outcome.</param>
/// <param name="StartedAt">The UTC start time.</param>
/// <param name="FinishedAt">The UTC end time.</param>
/// <param name="StartedBy">The actor that started the run.</param>
/// <param name="UsersRead">Entries read from the directory.</param>
/// <param name="UsersCreated">Users created.</param>
/// <param name="UsersUpdated">Users updated.</param>
/// <param name="UsersDeactivated">Users deactivated.</param>
/// <param name="MembershipChanges">Role / group membership changes.</param>
/// <param name="Errors">Entries that failed.</param>
/// <param name="Message">A user-safe message.</param>
public sealed record DirectorySyncSummary(
    Guid RunId,
    DirectorySyncTrigger Trigger,
    DirectorySyncStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    string? StartedBy,
    int UsersRead,
    int UsersCreated,
    int UsersUpdated,
    int UsersDeactivated,
    int MembershipChanges,
    int Errors,
    string? Message)
{
    /// <summary>Gets the duration, if finished.</summary>
    public TimeSpan? Duration => FinishedAt - StartedAt;

    /// <summary>Creates a summary from a persisted run.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The summary.</returns>
    public static DirectorySyncSummary From(DirectorySyncRun run) => new(
        run.Id, run.Trigger, run.Status, run.StartedAt, run.FinishedAt, run.StartedBy, run.UsersRead, run.UsersCreated,
        run.UsersUpdated, run.UsersDeactivated, run.MembershipChanges, run.Errors, run.Message);
}
