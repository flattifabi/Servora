using Servora.Domain.Backup;

namespace Servora.Application.Backup;

/// <summary>The backup settings as shown in the schedule editor. The password itself is never returned.</summary>
/// <param name="Schedule">A copy of the schedule and retention settings.</param>
/// <param name="PasswordConfigured">Whether an encryption password is stored in the secret store.</param>
/// <param name="DefaultDestination">The default destination folder (<c>&lt;data dir&gt;/backups/instance</c>).</param>
/// <param name="EffectiveDestination">The destination folder in use.</param>
/// <param name="MaxUploadBytes">The maximum size of an uploaded archive.</param>
public sealed record BackupSettingsView(BackupSchedule Schedule, bool PasswordConfigured, string DefaultDestination, string EffectiveDestination, long MaxUploadBytes);
