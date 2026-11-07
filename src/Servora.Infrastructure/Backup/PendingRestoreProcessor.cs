using Microsoft.Extensions.Logging;
using Servora.Application.Backup;
using Servora.Domain.Observability;

namespace Servora.Infrastructure.Backup;

/// <summary>
/// Executes a pending restore at startup, before database migrations and before any hosted service runs. On failure
/// the database is rolled back to the pre-restore safety backup and startup continues; only if that roll back fails
/// too is the exception rethrown, so the process exits with a non-zero code.
/// </summary>
/// <param name="requests">The restore request store.</param>
/// <param name="engine">The engine.</param>
/// <param name="settings">The settings store.</param>
/// <param name="operationLock">The single-flight lock.</param>
/// <param name="time">The time provider.</param>
/// <param name="logger">The logger.</param>
public sealed class PendingRestoreProcessor(
    RestoreRequestStore requests,
    BackupEngine engine,
    BackupSettingsStore settings,
    BackupOperationLock operationLock,
    TimeProvider time,
    ILogger<PendingRestoreProcessor> logger)
{
    /// <summary>Runs the pending restore, if any.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when a restore was executed successfully.</returns>
    public async Task<bool> RunAsync(CancellationToken cancellationToken = default)
    {
        RestoreTicket? ticket;
        try
        {
            ticket = requests.TryTake();
        }
        catch (BackupException ex)
        {
            logger.LogError(ex, "A pending restore request could not be read and was discarded");
            Record(false, "unknown", ex.Message, "System");
            return false;
        }

        if (ticket is null)
        {
            return false;
        }

        var request = ticket.Request;
        using var handle = await operationLock.AcquireAsync("Restore", TimeSpan.FromSeconds(30), cancellationToken);
        logger.LogWarning(
            "Restoring backup {Backup} requested by {User} at {RequestedAt:u} (attempt {Attempt})",
            request.BackupId, request.RequestedBy, request.RequestedAt, request.Attempts);

        var options = new BackupRestoreOptions { Password = ticket.Password };
        try
        {
            var result = await engine.RestoreAsync(request.BackupPath, options, cancellationToken);
            var message = $"Restored {result.Tables} tables ({result.Rows:N0} rows) and {result.Files} files from {request.BackupId}.";
            logger.LogWarning("{Message}", message);
            Record(true, request.BackupId, message, request.RequestedBy);
            requests.Complete();
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Restore of backup {Backup} failed", request.BackupId);
            var message = $"Restore of {request.BackupId} failed: {ex.Message}";
            if (options.DatabaseModified && request.SafetyBackupPath is { } safety && File.Exists(safety))
            {
                try
                {
                    logger.LogWarning("Rolling back to the pre-restore safety backup {Backup}", Path.GetFileName(safety));
                    await engine.RestoreAsync(safety, new BackupRestoreOptions { Password = ticket.SafetyPassword }, cancellationToken);
                    message += " The database was rolled back to the pre-restore safety backup.";
                }
                catch (Exception rollbackException) when (rollbackException is not OperationCanceledException)
                {
                    logger.LogCritical(rollbackException, "Rolling back to the pre-restore safety backup {Backup} failed", Path.GetFileName(safety));
                    message += $" Rolling back to the safety backup {Path.GetFileName(safety)} failed too: {rollbackException.Message}";
                    Record(false, request.BackupId, message, request.RequestedBy);
                    requests.Fail();
                    throw new BackupException(message + " Restore a backup with 'Servora backup restore <file>'.", rollbackException);
                }
            }

            Record(false, request.BackupId, message, request.RequestedBy);
            requests.Fail();
            return false;
        }
    }

    private void Record(bool succeeded, string backupId, string message, string actor)
    {
        var now = time.GetUtcNow();
        settings.UpdateState(s =>
        {
            s.LastRestore = new BackupRestoreStatus(now, succeeded, backupId, message);
            s.PendingAudits.Add(new PendingBackupAudit
            {
                Action = succeeded ? "backup.restored" : "backup.restore.failed",
                Outcome = succeeded ? AuditOutcome.Success : AuditOutcome.Failure,
                BackupId = backupId,
                Message = message,
                Actor = actor,
                At = now,
            });
        });
    }
}
