using Microsoft.Extensions.Logging;
using Servora.Application.Backup;
using Servora.Application.Common;
using Servora.Application.Observability;
using Servora.Application.Security;

namespace Servora.Infrastructure.Backup;

/// <summary>Guards the data reset: permission, typed confirmation, a backup first, audit entry and attachment clean-up.</summary>
/// <param name="engine">The engine that removes the records.</param>
/// <param name="backups">The backup service.</param>
/// <param name="currentUser">The current user.</param>
/// <param name="audit">The audit service.</param>
/// <param name="paths">The application paths.</param>
/// <param name="logger">The logger.</param>
public sealed class DataResetService(
    DataResetEngine engine,
    IBackupService backups,
    ICurrentUser currentUser,
    IAuditService audit,
    IAppPaths paths,
    ILogger<DataResetService> logger) : IDataResetService
{
    /// <inheritdoc />
    public async Task<Result<DataResetPreview>> GetPreviewAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Backup.Restore, cancellationToken))
        {
            return Result<DataResetPreview>.Forbidden();
        }

        return Result<DataResetPreview>.Success(await engine.CountAsync(currentUser.UserId, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result<DataResetResult>> ResetAsync(string confirmation, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Backup.Restore, cancellationToken))
        {
            return Result<DataResetResult>.Forbidden();
        }

        if (!string.Equals(confirmation, IDataResetService.ConfirmationText, StringComparison.Ordinal))
        {
            return Result<DataResetResult>.Invalid($"Type {IDataResetService.ConfirmationText} to confirm.");
        }

        var backup = await backups.CreateBackupAsync(cancellationToken);
        if (!backup.Succeeded)
        {
            return Result<DataResetResult>.Failure($"Nothing was removed because the backup failed: {backup.Error}");
        }

        var deleted = await engine.ResetAsync(currentUser.UserId, cancellationToken);
        RemoveAttachmentFiles();
        await audit.WriteAsync(
            new AuditEntry("backup", "data.reset")
            {
                TargetType = "instance",
                Message = $"Removed {deleted} records. Backup {backup.Value!.Id} was created first.",
            },
            cancellationToken);
        logger.LogWarning("Data reset removed {Rows} rows; backup {BackupId} was created first.", deleted, backup.Value.Id);
        return Result<DataResetResult>.Success(new DataResetResult(deleted, backup.Value.Id));
    }

    private void RemoveAttachmentFiles()
    {
        if (!Directory.Exists(paths.AttachmentDirectory))
        {
            return;
        }

        foreach (var entry in new DirectoryInfo(paths.AttachmentDirectory).EnumerateFileSystemInfos())
        {
            try
            {
                if (entry is DirectoryInfo directory)
                {
                    directory.Delete(recursive: true);
                }
                else
                {
                    entry.Delete();
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Data reset could not remove the attachment file {Path}.", entry.FullName);
            }
        }
    }
}
