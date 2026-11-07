namespace Servora.Infrastructure.Backup;

/// <summary>
/// Content of <c>&lt;data dir&gt;/restore-request.json</c>: a restore that runs at the next startup before migrations.
/// Passwords are protected with ASP.NET Core Data Protection (the key ring is merged, never replaced, by a restore).
/// </summary>
public sealed class RestoreRequest
{
    /// <summary>Gets or sets the archive to restore.</summary>
    public string BackupPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the archive identifier (file name).</summary>
    public string BackupId { get; set; } = string.Empty;

    /// <summary>Gets or sets the protected archive password, if the archive is encrypted.</summary>
    public string? ProtectedPassword { get; set; }

    /// <summary>Gets or sets the pre-restore safety backup used to roll back a failed restore.</summary>
    public string? SafetyBackupPath { get; set; }

    /// <summary>Gets or sets the protected password of the safety backup, if it is encrypted.</summary>
    public string? ProtectedSafetyPassword { get; set; }

    /// <summary>Gets or sets when the restore was requested.</summary>
    public DateTimeOffset RequestedAt { get; set; }

    /// <summary>Gets or sets who requested the restore.</summary>
    public string RequestedBy { get; set; } = string.Empty;

    /// <summary>Gets or sets how often the restore was started (protects against restart loops).</summary>
    public int Attempts { get; set; }
}
