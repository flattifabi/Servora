using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Servora.Application.Backup;
using Servora.Application.Common;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Domain.Backup;
using Servora.Domain.Observability;

namespace Servora.Infrastructure.Backup;

/// <summary>
/// <see cref="IBackupService"/> implementation: authorization (<c>backup.read</c> / <c>backup.manage</c> /
/// <c>backup.restore</c>), validation and auditing around the backup engine.
/// </summary>
/// <param name="currentUser">The current actor.</param>
/// <param name="audit">The audit service.</param>
/// <param name="secrets">The secret store (archive password).</param>
/// <param name="engine">The backup engine.</param>
/// <param name="catalog">The catalog.</param>
/// <param name="settings">The settings store.</param>
/// <param name="jobs">The backup job runner.</param>
/// <param name="operationLock">The single-flight lock.</param>
/// <param name="restoreRequests">The restore request store.</param>
/// <param name="restarter">The application restarter.</param>
/// <param name="database">The database access.</param>
/// <param name="configuration">The configuration (upload limit).</param>
/// <param name="time">The time provider.</param>
/// <param name="logger">The logger.</param>
public sealed class BackupService(
    ICurrentUser currentUser,
    IAuditService audit,
    ISecretStore secrets,
    BackupEngine engine,
    BackupCatalog catalog,
    BackupSettingsStore settings,
    BackupJobRunner jobs,
    BackupOperationLock operationLock,
    RestoreRequestStore restoreRequests,
    IApplicationRestarter restarter,
    IBackupDatabase database,
    IConfiguration configuration,
    TimeProvider time,
    ILogger<BackupService> logger) : IBackupService
{
    /// <summary>The configuration key of the upload limit in megabytes.</summary>
    public const string MaxUploadSizeKey = "Servora:Backup:MaxUploadSizeMb";

    private const long DefaultMaxUploadMb = 4096;

    private long MaxUploadBytes => Math.Max(1, configuration.GetValue<long?>(MaxUploadSizeKey) ?? DefaultMaxUploadMb) * 1024 * 1024;

    /// <inheritdoc />
    public async Task<Result<BackupOverview>> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Backup.Read, cancellationToken))
        {
            return Result<BackupOverview>.Forbidden();
        }

        var entries = catalog.List();
        var schedule = settings.GetSchedule();
        var state = settings.GetState();
        var lastValid = entries.FirstOrDefault(e => e.Manifest is not null && e.Integrity != BackupIntegrity.Invalid);
        var next = schedule.Enabled
            ? BackupScheduleCalculator.GetNextOccurrence(schedule, schedule.ResolveTimeZone(time.LocalTimeZone), time.GetUtcNow())
            : null;
        var busy = restoreRequests.IsPending ? "Restore pending – waiting for restart" : operationLock.CurrentOperation;

        return Result<BackupOverview>.Success(new BackupOverview(
            lastValid?.CreatedAt,
            lastValid?.Kind,
            next,
            schedule.Enabled,
            entries.Count,
            entries.Sum(e => e.SizeBytes),
            state.LastFailureAt,
            state.LastFailureMessage,
            state.LastRestore,
            busy,
            settings.ResolveDestination(schedule),
            restarter.RestartsAutomatically));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<BackupInfo>>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Backup.Read, cancellationToken))
        {
            return Result<IReadOnlyList<BackupInfo>>.Forbidden();
        }

        return Result<IReadOnlyList<BackupInfo>>.Success(catalog.List().Select(e => e.ToInfo()).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<BackupSettingsView>> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Backup.Read, cancellationToken))
        {
            return Result<BackupSettingsView>.Forbidden();
        }

        var schedule = settings.GetSchedule();
        var configured = await secrets.ExistsAsync(BackupConstants.PasswordSecretName, cancellationToken);
        return Result<BackupSettingsView>.Success(new BackupSettingsView(schedule, configured, settings.DefaultDestination, settings.ResolveDestination(schedule), MaxUploadBytes));
    }

    /// <inheritdoc />
    public async Task<Result> SaveSettingsAsync(BackupSettingsUpdate update, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Backup.Manage, cancellationToken))
        {
            return Result.Forbidden();
        }

        var schedule = update.Schedule.Clone();
        schedule.DestinationFolder = string.IsNullOrWhiteSpace(schedule.DestinationFolder) ? null : schedule.DestinationFolder.Trim();
        schedule.TimeZoneId = string.IsNullOrWhiteSpace(schedule.TimeZoneId) ? null : schedule.TimeZoneId;
        schedule.Weekdays = schedule.Weekdays.Distinct().OrderBy(d => d).ToList();
        var errors = schedule.Validate();
        if (errors.Count > 0)
        {
            return Result.Invalid(string.Join(" ", errors));
        }

        if (!string.IsNullOrEmpty(update.NewPassword) && update.NewPassword.Length < BackupConstants.MinPasswordLength)
        {
            return Result.Invalid($"The backup password must have at least {BackupConstants.MinPasswordLength} characters.");
        }

        var passwordWillExist = !update.ClearPassword && (!string.IsNullOrEmpty(update.NewPassword) || await secrets.ExistsAsync(BackupConstants.PasswordSecretName, cancellationToken));
        if (schedule.EncryptionEnabled && !passwordWillExist)
        {
            return Result.Invalid("Set a backup password to enable encryption.");
        }

        var destinationProblem = CheckDestination(settings.ResolveDestination(schedule));
        if (destinationProblem is not null)
        {
            return Result.Invalid(destinationProblem);
        }

        if (update.ClearPassword)
        {
            await secrets.DeleteAsync(BackupConstants.PasswordSecretName, cancellationToken);
        }
        else if (!string.IsNullOrEmpty(update.NewPassword))
        {
            await secrets.SetAsync(BackupConstants.PasswordSecretName, update.NewPassword, "Password that encrypts backup archives.", cancellationToken);
        }

        settings.SaveSchedule(schedule);
        await AuditAsync("backup.schedule.updated", AuditOutcome.Success, null, "Backup schedule and retention updated.", new
        {
            schedule.Enabled,
            Frequency = schedule.Frequency.ToString(),
            Weekdays = schedule.Weekdays.Select(d => d.ToString()).ToArray(),
            schedule.DayOfMonth,
            TimeOfDay = schedule.TimeOfDay.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
            schedule.TimeZoneId,
            schedule.DestinationFolder,
            schedule.IncludeAttachments,
            schedule.EncryptionEnabled,
            schedule.KeepLastScheduled,
            schedule.MaxAgeDays,
            schedule.KeepLastSafety,
            schedule.CatchUpMissedRuns,
            schedule.PreMigrationBackupEnabled,
            PasswordChanged = !string.IsNullOrEmpty(update.NewPassword),
            PasswordCleared = update.ClearPassword,
        }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<BackupInfo>> CreateBackupAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Backup.Manage, cancellationToken))
        {
            return Result<BackupInfo>.Forbidden();
        }

        if (restoreRequests.IsPending)
        {
            return Result<BackupInfo>.Conflict("A restore is pending. Wait until Servora has restarted.");
        }

        try
        {
            var result = await jobs.RunAsync(BackupKind.Manual, currentUser.DisplayName, cancellationToken);
            await AuditAsync("backup.created", AuditOutcome.Success, result.Entry.Id, "Manual backup created.", null, cancellationToken);
            return Result<BackupInfo>.Success(result.Entry.ToInfo());
        }
        catch (BackupException ex)
        {
            await AuditAsync("backup.failed", AuditOutcome.Failure, null, $"Manual backup failed: {ex.Message}", null, cancellationToken);
            return Result<BackupInfo>.Failure(ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<Result<BackupVerificationResult>> VerifyAsync(string backupId, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Backup.Read, cancellationToken))
        {
            return Result<BackupVerificationResult>.Forbidden();
        }

        var entry = catalog.Find(backupId);
        if (entry is null)
        {
            return Result<BackupVerificationResult>.NotFound("The backup was not found.");
        }

        var password = entry.Manifest?.Encryption is not null ? await secrets.GetAsync(BackupConstants.PasswordSecretName, cancellationToken) : null;
        if (entry.Manifest?.Encryption is { } encryption && !BackupCipher.IsPasswordValid(password, encryption))
        {
            password = null;
        }

        var result = await engine.VerifyAsync(entry.Path, password, cancellationToken);
        await AuditAsync("backup.verified", result.IsValid ? AuditOutcome.Success : AuditOutcome.Failure, entry.Id,
            result.IsValid ? $"Verified {result.EntriesChecked} entries." : "Verification failed: " + string.Join(" ", result.Problems.Take(3)), null, cancellationToken);
        return Result<BackupVerificationResult>.Success(result);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(string backupId, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Backup.Manage, cancellationToken))
        {
            return Result.Forbidden();
        }

        var entry = catalog.Find(backupId);
        if (entry is null)
        {
            return Result.NotFound("The backup was not found.");
        }

        using var handle = await operationLock.TryAcquireAsync("Delete backup", TimeSpan.FromSeconds(2), cancellationToken);
        if (handle is null)
        {
            return Result.Conflict("Another backup or restore operation is running. Try again later.");
        }

        try
        {
            File.Delete(entry.Path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result.Failure($"The backup could not be deleted: {ex.Message}");
        }

        catalog.Forget(entry.Path);
        await AuditAsync("backup.deleted", AuditOutcome.Success, entry.Id, $"Backup deleted ({entry.Kind}, created {entry.CreatedAt:u}).", null, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<BackupInfo>> ImportAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Backup.Manage, cancellationToken))
        {
            return Result<BackupInfo>.Forbidden();
        }

        var displayName = Path.GetFileName(fileName ?? string.Empty);
        if (!displayName.EndsWith(BackupConstants.FileExtension, StringComparison.OrdinalIgnoreCase))
        {
            return Result<BackupInfo>.Invalid($"Only {BackupConstants.FileExtension} files can be uploaded.");
        }

        var destination = settings.ResolveDestination(settings.GetSchedule());
        Directory.CreateDirectory(destination);
        var temp = Path.Combine(destination, $".upload-{Guid.NewGuid():N}.tmp");
        try
        {
            long size = 0;
            await using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    size += read;
                    if (size > MaxUploadBytes)
                    {
                        return Result<BackupInfo>.Invalid($"The file is larger than the upload limit of {MaxUploadBytes / (1024 * 1024):N0} MB.");
                    }

                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }

            BackupVerificationResult verification;
            try
            {
                using var reader = BackupArchiveReader.Open(temp);
                if (reader.IsEncrypted)
                {
                    var password = await secrets.GetAsync(BackupConstants.PasswordSecretName, cancellationToken);
                    if (BackupCipher.IsPasswordValid(password, reader.Manifest.Encryption!))
                    {
                        reader.Unlock(password);
                    }
                }

                verification = await reader.VerifyAsync(cancellationToken);
            }
            catch (BackupException ex)
            {
                await AuditAsync("backup.uploaded", AuditOutcome.Failure, null, $"Rejected upload '{displayName}': {ex.Message}", null, cancellationToken);
                return Result<BackupInfo>.Invalid(ex.Message);
            }

            if (!verification.IsValid)
            {
                await AuditAsync("backup.uploaded", AuditOutcome.Failure, null, $"Rejected upload '{displayName}': integrity check failed.", null, cancellationToken);
                return Result<BackupInfo>.Invalid("The archive failed the integrity check: " + string.Join(" ", verification.Problems.Take(3)));
            }

            var finalPath = BackupFileNames.CreatePath(destination, time.GetUtcNow(), BackupKind.Uploaded);
            File.Move(temp, finalPath);
            catalog.RecordVerification(finalPath, valid: true);
            var entry = catalog.Describe(finalPath);
            await AuditAsync("backup.uploaded", AuditOutcome.Success, entry.Id, $"Uploaded '{displayName}' ({size:N0} bytes).", null, cancellationToken);
            return Result<BackupInfo>.Success(entry.ToInfo());
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }

    /// <inheritdoc />
    public async Task<Result<BackupDownload>> OpenDownloadAsync(string backupId, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Backup.Manage, cancellationToken))
        {
            return Result<BackupDownload>.Forbidden();
        }

        var entry = catalog.Find(backupId);
        if (entry is null)
        {
            return Result<BackupDownload>.NotFound("The backup was not found.");
        }

        var stream = new FileStream(entry.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        await AuditAsync("backup.downloaded", AuditOutcome.Success, entry.Id, "Backup downloaded.", null, cancellationToken);
        return Result<BackupDownload>.Success(new BackupDownload(entry.Id, stream, stream.Length));
    }

    /// <inheritdoc />
    public async Task<Result<BackupRestoreCheck>> CheckRestoreAsync(string backupId, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Backup.Restore, cancellationToken))
        {
            return Result<BackupRestoreCheck>.Forbidden();
        }

        var entry = catalog.Find(backupId);
        if (entry is null)
        {
            return Result<BackupRestoreCheck>.NotFound("The backup was not found.");
        }

        var problems = new List<string>();
        var storedPasswordWorks = false;
        if (entry.Manifest is null)
        {
            problems.Add(entry.Error ?? "The archive cannot be read.");
        }
        else
        {
            problems.AddRange(engine.CheckCompatibility(entry.Manifest));
            if (entry.Manifest.Encryption is { } encryption)
            {
                storedPasswordWorks = BackupCipher.IsPasswordValid(await secrets.GetAsync(BackupConstants.PasswordSecretName, cancellationToken), encryption);
            }
        }

        if (entry.Integrity == BackupIntegrity.Invalid)
        {
            problems.Add("The archive failed verification.");
        }

        if (restoreRequests.IsPending)
        {
            problems.Add("Another restore is already pending.");
        }

        return Result<BackupRestoreCheck>.Success(new BackupRestoreCheck(
            entry.ToInfo(), problems.Count == 0, problems, entry.Manifest?.Encryption is not null, storedPasswordWorks, restarter.RestartsAutomatically));
    }

    /// <inheritdoc />
    public async Task<Result> RequestRestoreAsync(BackupRestoreRequestInput input, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Backup.Restore, cancellationToken))
        {
            return Result.Forbidden();
        }

        if (!string.Equals(input.Confirmation?.Trim(), BackupConstants.RestoreConfirmationText, StringComparison.Ordinal))
        {
            return Result.Invalid($"Type {BackupConstants.RestoreConfirmationText} to confirm the restore.");
        }

        var entry = catalog.Find(input.BackupId);
        if (entry is null)
        {
            return Result.NotFound("The backup was not found.");
        }

        if (restoreRequests.IsPending)
        {
            return Result.Conflict("A restore is already pending.");
        }

        var password = string.IsNullOrEmpty(input.Password) ? await secrets.GetAsync(BackupConstants.PasswordSecretName, cancellationToken) : input.Password;
        try
        {
            using var reader = BackupArchiveReader.Open(entry.Path);
            if (reader.IsEncrypted)
            {
                reader.Unlock(password);
            }
            else
            {
                password = null;
            }

            var verification = await reader.VerifyAsync(cancellationToken);
            catalog.RecordVerification(entry.Path, verification.IsValid);
            if (!verification.IsValid)
            {
                return Result.Failure("The backup failed verification and cannot be restored: " + string.Join(" ", verification.Problems.Take(3)));
            }

            var problems = engine.CheckCompatibility(reader.Manifest);
            if (problems.Count > 0)
            {
                return Result.Failure(string.Join(" ", problems));
            }
        }
        catch (BackupPasswordException ex)
        {
            return Result.Invalid(ex.Message);
        }
        catch (BackupException ex)
        {
            return Result.Failure(ex.Message);
        }

        using var handle = await operationLock.TryAcquireAsync("Restore preparation", TimeSpan.FromSeconds(5), cancellationToken);
        if (handle is null)
        {
            return Result.Conflict("Another backup or restore operation is running. Try again later.");
        }

        BackupJobResult safety;
        try
        {
            safety = await jobs.RunLockedAsync(BackupKind.PreRestore, $"{currentUser.DisplayName} (before restoring {entry.Id})", cancellationToken);
        }
        catch (BackupException ex)
        {
            await AuditAsync("backup.restore.requested", AuditOutcome.Failure, entry.Id, $"Restore not started: the pre-restore safety backup failed ({ex.Message}).", null, cancellationToken);
            return Result.Failure($"The pre-restore safety backup failed, so the restore was not started: {ex.Message}");
        }

        restoreRequests.Write(
            new RestoreRequest
            {
                BackupPath = entry.Path,
                BackupId = entry.Id,
                SafetyBackupPath = safety.Entry.Path,
                RequestedAt = time.GetUtcNow(),
                RequestedBy = currentUser.DisplayName,
            },
            password,
            safety.Password);

        await AuditAsync("backup.restore.requested", AuditOutcome.Success, entry.Id, $"Restore requested; safety backup {safety.Entry.Id} created. Servora restarts to restore.", null, cancellationToken);
        logger.LogWarning("Restore of {Backup} requested by {User}; safety backup {Safety}", entry.Id, currentUser.DisplayName, safety.Entry.Id);
        restarter.RequestRestart($"restore of backup {entry.Id}");
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<DatabaseSchemaStatus>> GetSchemaStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Backup.Read, cancellationToken))
        {
            return Result<DatabaseSchemaStatus>.Forbidden();
        }

        await using var db = database.CreateContext();
        var known = db.Database.GetMigrations().ToList();
        var applied = known.Count == 0 ? [] : (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
        var pending = known.Except(applied, StringComparer.Ordinal).ToList();
        var unknown = applied.Except(known, StringComparer.Ordinal).ToList();
        var problem = unknown.Count > 0
            ? $"The database contains migrations unknown to this version (it was upgraded by a newer version): {string.Join(", ", unknown)}."
            : null;

        return Result<DatabaseSchemaStatus>.Success(new DatabaseSchemaStatus(
            database.ProviderKey, known.Count > 0, applied.LastOrDefault(), applied, pending, known.Count, problem));
    }

    private static string? CheckDestination(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            var probe = Path.Combine(folder, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "servora");
            File.Delete(probe);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return $"The destination folder '{folder}' is not writable for the Servora service account: {ex.Message}";
        }
    }

    private Task AuditAsync(string action, AuditOutcome outcome, string? backupId, string message, object? details, CancellationToken cancellationToken) =>
        audit.WriteAsync(
            new AuditEntry("backup", action, outcome)
            {
                TargetType = "backup",
                TargetId = backupId,
                TargetName = backupId,
                Message = message,
                Details = details,
            },
            cancellationToken);
}
