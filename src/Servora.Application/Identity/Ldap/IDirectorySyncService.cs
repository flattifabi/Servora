using Servora.Domain.Identity;

namespace Servora.Application.Identity.Ldap;

/// <summary>
/// Runs a directory synchronisation: reads users (paged, bounded), provisions / updates local users, reconciles mapped
/// roles and groups and deactivates directory users that were disabled or removed. Backend service used by the
/// background scheduler (system actor) and by <see cref="IDirectoryAdminService.SyncNowAsync"/> after its permission check.
/// Only one run executes at a time.
/// </summary>
public interface IDirectorySyncService
{
    /// <summary>Runs a synchronisation.</summary>
    /// <param name="trigger">What started the run.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The run summary.</returns>
    Task<DirectorySyncSummary> RunAsync(DirectorySyncTrigger trigger, CancellationToken cancellationToken = default);

    /// <summary>Gets the most recent run, if any.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The last run summary.</returns>
    Task<DirectorySyncSummary?> GetLastRunAsync(CancellationToken cancellationToken = default);
}
