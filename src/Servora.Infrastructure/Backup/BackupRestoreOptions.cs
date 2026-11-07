namespace Servora.Infrastructure.Backup;

/// <summary>Options of <see cref="BackupEngine.RestoreAsync"/>.</summary>
public sealed class BackupRestoreOptions
{
    /// <summary>Gets or sets the archive password.</summary>
    public string? Password { get; set; }

    /// <summary>Gets or sets a value indicating whether key ring files, attachments and data files are restored too.</summary>
    public bool RestoreFiles { get; set; } = true;

    /// <summary>Gets a value indicating whether the restore has started to change the database (set by the engine).</summary>
    public bool DatabaseModified { get; internal set; }
}
