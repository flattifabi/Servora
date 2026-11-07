namespace Servora.Application.Backup;

/// <summary>Input for <see cref="IBackupService.RequestRestoreAsync"/>.</summary>
/// <param name="BackupId">The backup to restore.</param>
/// <param name="Confirmation">The confirmation text; must equal <see cref="BackupConstants.RestoreConfirmationText"/>.</param>
/// <param name="Password">The archive password when it differs from the configured backup password.</param>
public sealed record BackupRestoreRequestInput(string BackupId, string Confirmation, string? Password = null);
