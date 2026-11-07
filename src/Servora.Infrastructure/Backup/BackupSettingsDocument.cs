using Servora.Domain.Backup;

namespace Servora.Infrastructure.Backup;

/// <summary>Content of <c>&lt;data dir&gt;/backup.json</c>.</summary>
public sealed class BackupSettingsDocument
{
    /// <summary>Gets or sets the document version.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Gets or sets the schedule and retention settings.</summary>
    public BackupSchedule Schedule { get; set; } = new();

    /// <summary>Gets or sets the runtime state.</summary>
    public BackupRunState State { get; set; } = new();
}
