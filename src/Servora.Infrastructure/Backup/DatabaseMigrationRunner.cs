using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Servora.Domain.Backup;
using Servora.Domain.Observability;

namespace Servora.Infrastructure.Backup;

/// <summary>
/// Brings the database schema up to date at startup, guarded by backups:
/// <list type="number">
/// <item>no migrations shipped (development builds) → <c>EnsureCreated</c>;</item>
/// <item>database at a migration unknown to this version → stop (downgrade without restore);</item>
/// <item>tables but no migration history (legacy <c>EnsureCreated</c> database) → stop with instructions;</item>
/// <item>pending migrations on an existing database → <see cref="BackupKind.PreMigration"/> backup first (configurable),
/// then migrate; if a migration fails the backup is restored automatically and startup stops with a non-zero exit code,
/// so the installer's health check rolls back the program files as well.</item>
/// </list>
/// </summary>
/// <param name="database">The database access.</param>
/// <param name="jobs">The backup job runner.</param>
/// <param name="engine">The engine.</param>
/// <param name="settings">The settings store.</param>
/// <param name="operationLock">The single-flight lock.</param>
/// <param name="time">The time provider.</param>
/// <param name="logger">The logger.</param>
public sealed class DatabaseMigrationRunner(
    IBackupDatabase database,
    BackupJobRunner jobs,
    BackupEngine engine,
    BackupSettingsStore settings,
    BackupOperationLock operationLock,
    TimeProvider time,
    ILogger<DatabaseMigrationRunner> logger)
{
    /// <summary>The documentation anchor for legacy databases.</summary>
    public const string LegacyDatabaseHelp = "docs/operations/upgrade.md#legacy-databases-without-migration-history";

    /// <summary>Migrates the database.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the schema is up to date.</returns>
    /// <exception cref="DatabaseMigrationException">Thrown when startup must stop.</exception>
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await using var db = database.CreateContext();
        var migrations = db.Database.GetMigrations().ToList();
        if (migrations.Count == 0)
        {
            var created = await db.Database.EnsureCreatedAsync(cancellationToken);
            logger.LogInformation(created ? "Database schema created (no migrations in this build)." : "Database schema already exists (no migrations in this build).");
            return;
        }

        var applied = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
        var unknown = applied.Except(migrations, StringComparer.Ordinal).ToList();
        if (unknown.Count > 0)
        {
            throw new DatabaseMigrationException(
                $"The database contains migration(s) {string.Join(", ", unknown)} that this Servora version ({BackupEngine.AppVersion}) does not know: it was upgraded by a newer version. " +
                "Install that version (or newer) again, or restore a backup created by this version with 'Servora backup restore <file>'.");
        }

        var pending = migrations.Except(applied, StringComparer.Ordinal).ToList();
        if (pending.Count == 0)
        {
            logger.LogDebug("Database schema is up to date ({Migration}).", applied.LastOrDefault());
            return;
        }

        if (applied.Count == 0)
        {
            if (await HasTablesAsync(db, cancellationToken))
            {
                throw new DatabaseMigrationException(
                    "The database contains Servora tables but no migration history. It was created by a pre-release build without migrations (EnsureCreated) " +
                    "and cannot be upgraded automatically. Point Servora to a new, empty database (or move servora.db away for SQLite) to create a fresh schema, " +
                    $"or baseline the existing schema as described in {LegacyDatabaseHelp}.");
            }

            logger.LogInformation("Creating the database schema ({Count} migration(s)).", pending.Count);
            await db.Database.MigrateAsync(cancellationToken);
            return;
        }

        logger.LogWarning("Database schema is at {Current}; applying {Count} migration(s): {Migrations}", applied[^1], pending.Count, string.Join(", ", pending));
        BackupJobResult? safety = null;
        if (settings.GetSchedule().PreMigrationBackupEnabled)
        {
            try
            {
                using var handle = await operationLock.AcquireAsync("Pre-migration backup", TimeSpan.FromMinutes(2), cancellationToken);
                safety = await jobs.RunLockedAsync(BackupKind.PreMigration, $"System (before migrating to {pending[^1]})", cancellationToken);
                AddPendingAudit("backup.created", AuditOutcome.Success, safety.Entry.Id, $"Pre-migration backup before applying {pending.Count} migration(s).");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                AddPendingAudit("backup.failed", AuditOutcome.Failure, null, $"Pre-migration backup failed: {ex.Message}");
                throw new DatabaseMigrationException(
                    $"The pre-migration backup failed, so no migration was applied: {ex.Message} Fix the cause (see the log) and start Servora again. " +
                    "If you have another current backup you can disable 'Back up before migrations' in backup.json (preMigrationBackupEnabled).", ex);
            }
        }
        else
        {
            logger.LogWarning("Automatic pre-migration backups are disabled; migrating without a safety backup.");
        }

        try
        {
            await db.Database.MigrateAsync(cancellationToken);
            logger.LogInformation("Database migrated to {Migration}.", pending[^1]);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogCritical(ex, "Database migration failed.");
            if (safety is null)
            {
                throw new DatabaseMigrationException(
                    $"Applying database migrations failed: {ex.Message} No pre-migration backup exists; restore a backup with 'Servora backup restore <file>' before starting the previous version.", ex);
            }

            try
            {
                using var handle = await operationLock.AcquireAsync("Restore after failed migration", TimeSpan.FromMinutes(2), cancellationToken);
                await engine.RestoreAsync(safety.Entry.Path, new BackupRestoreOptions { Password = safety.Password }, cancellationToken);
            }
            catch (Exception restoreException) when (restoreException is not OperationCanceledException)
            {
                logger.LogCritical(restoreException, "Restoring the pre-migration backup {Backup} failed.", safety.Entry.Id);
                AddPendingAudit("backup.restore.failed", AuditOutcome.Failure, safety.Entry.Id, $"Automatic restore after a failed migration failed: {restoreException.Message}");
                throw new DatabaseMigrationException(
                    $"Applying database migrations failed ({ex.Message}) and restoring the pre-migration backup {safety.Entry.Id} failed too ({restoreException.Message}). " +
                    $"Restore it manually with 'Servora backup restore {safety.Entry.Id}' using the previous Servora version.",
                    new AggregateException(ex, restoreException));
            }

            var message = $"Automatic restore of {safety.Entry.Id} after a failed migration.";
            logger.LogWarning("The database was restored from the pre-migration backup {Backup}.", safety.Entry.Id);
            AddPendingAudit("backup.restored", AuditOutcome.Success, safety.Entry.Id, message);
            settings.UpdateState(s => s.LastRestore = new Application.Backup.BackupRestoreStatus(time.GetUtcNow(), true, safety.Entry.Id, message));
            throw new DatabaseMigrationException(
                $"Applying database migrations failed: {ex.Message} The database was restored automatically from the pre-migration backup {safety.Entry.Id}; " +
                "the previous Servora version can be started again (the installer rolls the program files back automatically).", ex);
        }
    }

    private static async Task<bool> HasTablesAsync(DbContext db, CancellationToken cancellationToken)
    {
        var creator = db.GetService<IRelationalDatabaseCreator>();
        return await creator.ExistsAsync(cancellationToken) && await creator.HasTablesAsync(cancellationToken);
    }

    private void AddPendingAudit(string action, AuditOutcome outcome, string? backupId, string message)
    {
        var now = time.GetUtcNow();
        settings.UpdateState(s => s.PendingAudits.Add(new PendingBackupAudit
        {
            Action = action,
            Outcome = outcome,
            BackupId = backupId,
            Message = message,
            Actor = "System",
            At = now,
        }));
    }
}
