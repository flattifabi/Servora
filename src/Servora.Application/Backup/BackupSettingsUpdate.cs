using Servora.Domain.Backup;

namespace Servora.Application.Backup;

/// <summary>Input for <see cref="IBackupService.SaveSettingsAsync"/>.</summary>
/// <param name="Schedule">The schedule and retention settings.</param>
/// <param name="NewPassword">A new encryption password, or <see langword="null"/> to keep the stored one.</param>
/// <param name="ClearPassword">Whether to delete the stored encryption password.</param>
public sealed record BackupSettingsUpdate(BackupSchedule Schedule, string? NewPassword = null, bool ClearPassword = false);
