namespace Servora.Infrastructure.Backup;

/// <summary>Parsed arguments of <c>Servora backup …</c>.</summary>
public sealed class BackupCommandLineOptions
{
    /// <summary>Gets or sets the command: <c>list</c>, <c>create</c>, <c>verify</c>, <c>restore</c> or <c>help</c>.</summary>
    public string Command { get; set; } = "help";

    /// <summary>Gets or sets the archive (catalog file name or path) of <c>verify</c> / <c>restore</c>.</summary>
    public string? File { get; set; }

    /// <summary>Gets or sets the archive password (<c>--password</c>, or the <c>SERVORA_BACKUP_PASSWORD</c> environment variable).</summary>
    public string? Password { get; set; }

    /// <summary>Gets or sets the data directory (<c>--data-dir</c>).</summary>
    public string? DataDirectory { get; set; }

    /// <summary>Gets or sets a value indicating whether the pre-restore safety backup is skipped (<c>--no-safety-backup</c>).</summary>
    public bool NoSafetyBackup { get; set; }

    /// <summary>Gets or sets a value indicating whether the interactive confirmation is skipped (<c>--yes</c>).</summary>
    public bool AssumeYes { get; set; }

    /// <summary>Gets or sets a value indicating whether attachments are excluded from <c>create</c> (<c>--no-attachments</c>).</summary>
    public bool NoAttachments { get; set; }

    /// <summary>Gets the usage errors.</summary>
    public List<string> Errors { get; } = [];
}
