using Microsoft.Extensions.Logging;
using Servora.Domain.Backup;

namespace Servora.Infrastructure.Backup;

/// <summary>
/// Runs a backup with the configured settings (destination, attachments, encryption), records the outcome in
/// <c>backup.json</c> and applies retention. Shared by "Back up now", the scheduler, pre-migration and pre-restore
/// safety backups and the command line.
/// </summary>
/// <param name="engine">The engine.</param>
/// <param name="settings">The settings store.</param>
/// <param name="operationLock">The single-flight lock.</param>
/// <param name="retention">The retention service.</param>
/// <param name="passwords">The password source.</param>
/// <param name="time">The time provider.</param>
/// <param name="logger">The logger.</param>
public sealed class BackupJobRunner(
    BackupEngine engine,
    BackupSettingsStore settings,
    BackupOperationLock operationLock,
    BackupRetentionService retention,
    IBackupPasswordSource passwords,
    TimeProvider time,
    ILogger<BackupJobRunner> logger)
{
    /// <summary>Gets or sets the PBKDF2 iteration count for encrypted archives (lowered only by tests).</summary>
    public int Iterations { get; set; } = BackupCipher.DefaultIterations;

    /// <summary>Acquires the operation lock and runs a backup.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="createdBy">Who or what triggers the backup.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="passwordOverride">A password that replaces the configured one (command line), or <see langword="null"/>.</param>
    /// <param name="includeAttachments">Overrides the configured attachment setting, or <see langword="null"/>.</param>
    /// <returns>The result.</returns>
    /// <exception cref="BackupException">Thrown when another operation runs or the backup fails.</exception>
    public async Task<BackupJobResult> RunAsync(BackupKind kind, string createdBy, CancellationToken cancellationToken, string? passwordOverride = null, bool? includeAttachments = null)
    {
        using var handle = await operationLock.AcquireAsync($"{kind} backup", TimeSpan.FromSeconds(2), cancellationToken);
        return await RunLockedAsync(kind, createdBy, cancellationToken, passwordOverride, includeAttachments);
    }

    /// <summary>Runs a backup while the caller holds the operation lock.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="createdBy">Who or what triggers the backup.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="passwordOverride">A password that replaces the configured one (command line), or <see langword="null"/>.</param>
    /// <param name="includeAttachments">Overrides the configured attachment setting, or <see langword="null"/>.</param>
    /// <returns>The result.</returns>
    public async Task<BackupJobResult> RunLockedAsync(BackupKind kind, string createdBy, CancellationToken cancellationToken, string? passwordOverride = null, bool? includeAttachments = null)
    {
        var schedule = settings.GetSchedule();
        if (includeAttachments is { } attachments)
        {
            schedule.IncludeAttachments = attachments;
        }

        var isSafetyBackup = kind is BackupKind.PreMigration or BackupKind.PreRestore;
        settings.UpdateState(s => s.LastAttemptAt = time.GetUtcNow());
        try
        {
            var password = string.IsNullOrEmpty(passwordOverride) ? await ResolvePasswordAsync(schedule, isSafetyBackup, cancellationToken) : passwordOverride;
            var destination = ResolveDestination(schedule, isSafetyBackup);
            var entry = await engine.CreateAsync(
                new BackupCreateOptions(kind, createdBy, destination)
                {
                    IncludeAttachments = schedule.IncludeAttachments,
                    IncludeInstanceSettings = schedule.IncludeInstanceSettings,
                    Password = password,
                    Iterations = Iterations,
                },
                cancellationToken);

            settings.UpdateState(s =>
            {
                s.LastSuccessAt = entry.CreatedAt;
                s.LastSuccessId = entry.Id;
                s.LastFailureAt = null;
                s.LastFailureMessage = null;
            });

            try
            {
                retention.Apply(schedule);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BackupException)
            {
                logger.LogWarning(ex, "Applying backup retention failed");
            }

            return new BackupJobResult(entry, password);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var message = ex is BackupException ? ex.Message : $"{kind} backup failed: {ex.Message}";
            if (ex is BackupException)
            {
                logger.LogError("{Kind} backup failed: {Message}", kind, ex.Message);
            }
            else
            {
                logger.LogError(ex, "{Kind} backup failed", kind);
            }
            settings.UpdateState(s =>
            {
                s.LastFailureAt = time.GetUtcNow();
                s.LastFailureMessage = message.Length > 1000 ? message[..1000] : message;
            });
            throw ex as BackupException ?? new BackupException(message, ex);
        }
    }

    private async Task<string?> ResolvePasswordAsync(BackupSchedule schedule, bool isSafetyBackup, CancellationToken cancellationToken)
    {
        if (!schedule.EncryptionEnabled)
        {
            return null;
        }

        string? password;
        try
        {
            password = await passwords.GetPasswordAsync(cancellationToken);
        }
        catch (Exception ex) when (isSafetyBackup && ex is not OperationCanceledException)
        {
            // Safety backups must not be blocked (for example the secret table of an old schema is unreadable).
            logger.LogWarning(ex, "The backup password could not be read; the safety backup is created without encryption");
            return null;
        }

        if (string.IsNullOrEmpty(password))
        {
            if (isSafetyBackup)
            {
                logger.LogWarning("Backup encryption is enabled but no password is configured; the safety backup is created without encryption");
                return null;
            }

            throw new BackupException("Backup encryption is enabled but no backup password is configured.");
        }

        return password;
    }

    private string ResolveDestination(BackupSchedule schedule, bool isSafetyBackup)
    {
        var destination = settings.ResolveDestination(schedule);
        try
        {
            Directory.CreateDirectory(destination);
            return destination;
        }
        catch (Exception ex) when (isSafetyBackup && ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Backup destination {Destination} is not available; the safety backup is written to {Fallback}", destination, settings.DefaultDestination);
            Directory.CreateDirectory(settings.DefaultDestination);
            return settings.DefaultDestination;
        }
    }
}
