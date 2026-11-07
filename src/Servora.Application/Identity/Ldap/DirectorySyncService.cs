using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Domain.Identity;
using Servora.Domain.Observability;

namespace Servora.Application.Identity.Ldap;

/// <summary>Default <see cref="IDirectorySyncService"/>.</summary>
/// <param name="configuration">The directory configuration provider.</param>
/// <param name="client">The directory client.</param>
/// <param name="provisioning">The provisioning service.</param>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="currentUser">The current actor (recorded as "started by").</param>
/// <param name="permissions">The permission service (cache invalidation).</param>
/// <param name="audit">The audit service.</param>
/// <param name="clock">The clock.</param>
/// <param name="sessionInvalidators">Host session invalidators (optional).</param>
public sealed class DirectorySyncService(
    IDirectoryConfigurationProvider configuration,
    IDirectoryClient client,
    IDirectoryProvisioningService provisioning,
    IAppDbContextFactory dbFactory,
    ICurrentUser currentUser,
    IPermissionService permissions,
    IAuditService audit,
    IClock clock,
    IEnumerable<IUserSessionInvalidator> sessionInvalidators) : IDirectorySyncService
{
    private const int MaxLocalDirectoryUsers = 100_000;
    private static readonly SemaphoreSlim s_gate = new(1, 1);

    /// <inheritdoc />
    public async Task<DirectorySyncSummary> RunAsync(DirectorySyncTrigger trigger, CancellationToken cancellationToken = default)
    {
        var settings = await configuration.GetSettingsAsync(cancellationToken);
        if (!settings.Enabled || !settings.IsConnectionConfigured)
        {
            return NotStarted(trigger, "The directory integration is not enabled or not fully configured.");
        }

        if (!await s_gate.WaitAsync(0, cancellationToken))
        {
            return NotStarted(trigger, "A directory synchronisation is already running.");
        }

        try
        {
            return await RunCoreAsync(settings, trigger, cancellationToken);
        }
        finally
        {
            s_gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<DirectorySyncSummary?> GetLastRunAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var run = await db.Set<DirectorySyncRun>().AsNoTracking().OrderByDescending(r => r.StartedAt).FirstOrDefaultAsync(cancellationToken);
        return run is null ? null : DirectorySyncSummary.From(run);
    }

    private async Task<DirectorySyncSummary> RunCoreAsync(DirectorySettings settings, DirectorySyncTrigger trigger, CancellationToken cancellationToken)
    {
        var run = new DirectorySyncRun { StartedAt = clock.UtcNow, Trigger = trigger, StartedBy = currentUser.DisplayName };
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            db.Set<DirectorySyncRun>().Add(run);
            await db.SaveChangesAsync(cancellationToken);
        }

        try
        {
            var bindPassword = await configuration.GetBindPasswordAsync(cancellationToken);
            var search = await client.SearchUsersAsync(settings, bindPassword, settings.MaxSyncEntries, cancellationToken);
            if (!search.Succeeded)
            {
                run.Status = DirectorySyncStatus.Failed;
                run.Message = search.Message ?? "The directory search failed.";
                return await FinishAsync(run, cancellationToken);
            }

            run.UsersRead = search.Users.Count;
            var mappings = await configuration.GetActiveMappingsAsync(cancellationToken);
            var seenExternalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenUserNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in search.Users)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.IsNullOrWhiteSpace(entry.ExternalId))
                {
                    seenExternalIds.Add(entry.ExternalId);
                }

                seenUserNames.Add(entry.UserName);
                try
                {
                    var result = await provisioning.ProvisionAsync(entry, settings, mappings, settings.SyncImportsNewUsers, cancellationToken);
                    if (result.Skipped)
                    {
                        continue;
                    }

                    if (!result.Succeeded)
                    {
                        run.Errors++;
                        continue;
                    }

                    run.UsersCreated += result.Created ? 1 : 0;
                    run.UsersUpdated += result.Updated ? 1 : 0;
                    run.UsersDeactivated += result.Deactivated ? 1 : 0;
                    run.MembershipChanges += result.MembershipChanges;
                }
                catch (DbUpdateException)
                {
                    run.Errors++;
                }
            }

            var warnings = new List<string>();
            if (settings.DeactivateMissingUsers && !search.Truncated)
            {
                if (search.Users.Count > 0)
                {
                    run.UsersDeactivated += await DeactivateMissingAsync(seenExternalIds, seenUserNames, cancellationToken);
                }
                else
                {
                    // An empty result usually means a wrong search base or filter: never mass-deactivate on that basis.
                    warnings.Add("The directory returned no users; no accounts were deactivated. Check the user search base and filter.");
                }
            }

            if (search.Truncated)
            {
                warnings.Add($"Only the first {settings.MaxSyncEntries:N0} entries were read; missing users were not deactivated.");
            }

            if (run.Errors > 0)
            {
                warnings.Add($"{run.Errors} entr{(run.Errors == 1 ? "y" : "ies")} could not be synchronised (see the audit log).");
            }

            run.Status = warnings.Count > 0 ? DirectorySyncStatus.CompletedWithWarnings : DirectorySyncStatus.Succeeded;
            run.Message = warnings.Count > 0
                ? string.Join(' ', warnings)
                : $"Read {run.UsersRead:N0} entries: {run.UsersCreated} created, {run.UsersUpdated} updated, {run.UsersDeactivated} deactivated.";
            return await FinishAsync(run, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            run.Status = DirectorySyncStatus.Failed;
            run.Message = "The synchronisation was cancelled.";
            return await FinishAsync(run, CancellationToken.None);
        }
    }

    private async Task<int> DeactivateMissingAsync(HashSet<string> seenExternalIds, HashSet<string> seenUserNames, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var candidates = await db.Set<User>()
            .Where(u => u.Source == UserSource.Directory && u.IsActive)
            .OrderBy(u => u.UserName)
            .Take(MaxLocalDirectoryUsers)
            .ToListAsync(cancellationToken);

        var deactivated = new List<User>();
        foreach (var user in candidates)
        {
            // Lenient on purpose: a user counts as present when either the stable id or the login name was seen.
            var seen = seenUserNames.Contains(user.UserName) || (user.ExternalId is { Length: > 0 } id && seenExternalIds.Contains(id));
            if (!seen)
            {
                user.IsActive = false;
                user.SecurityStamp = Guid.NewGuid().ToString("N");
                deactivated.Add(user);
            }
        }

        if (deactivated.Count == 0)
        {
            return 0;
        }

        await db.SaveChangesAsync(cancellationToken);
        foreach (var user in deactivated)
        {
            permissions.Invalidate(user.Id);
            foreach (var invalidator in sessionInvalidators)
            {
                invalidator.Invalidate(user.Id);
            }

            await audit.WriteAsync(new AuditEntry("identity", "user.deactivated")
            {
                TargetType = "sys_user",
                TargetId = user.Id.ToString(),
                TargetName = user.UserName,
                Message = "Deactivated by directory synchronisation: the account no longer matches the directory user filter.",
            }, cancellationToken);
        }

        return deactivated.Count;
    }

    private async Task<DirectorySyncSummary> FinishAsync(DirectorySyncRun run, CancellationToken cancellationToken)
    {
        run.FinishedAt = clock.UtcNow;
        if (run.Message is { Length: > 4000 })
        {
            run.Message = run.Message[..4000];
        }

        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            db.Set<DirectorySyncRun>().Update(run);
            await db.SaveChangesAsync(cancellationToken);
        }

        await audit.WriteAsync(new AuditEntry("identity", "directory.sync", run.Status == DirectorySyncStatus.Failed ? AuditOutcome.Failure : AuditOutcome.Success)
        {
            TargetType = "sys_directory_sync_run",
            TargetId = run.Id.ToString(),
            Message = run.Message,
            Details = new { run.Trigger, run.UsersRead, run.UsersCreated, run.UsersUpdated, run.UsersDeactivated, run.MembershipChanges, run.Errors },
        }, cancellationToken);
        return DirectorySyncSummary.From(run);
    }

    private DirectorySyncSummary NotStarted(DirectorySyncTrigger trigger, string message)
    {
        var now = clock.UtcNow;
        return new DirectorySyncSummary(Guid.Empty, trigger, DirectorySyncStatus.Failed, now, now, currentUser.DisplayName, 0, 0, 0, 0, 0, 0, message);
    }
}
