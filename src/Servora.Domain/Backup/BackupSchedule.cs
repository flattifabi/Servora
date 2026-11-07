namespace Servora.Domain.Backup;

/// <summary>
/// Backup schedule and retention settings. Stored in <c>&lt;data dir&gt;/backup.json</c> (not in the database) so the
/// configuration survives a restore of an older backup.
/// </summary>
public sealed class BackupSchedule
{
    /// <summary>Maximum supported retention count.</summary>
    public const int MaxKeep = 1000;

    /// <summary>Maximum supported retention age in days.</summary>
    public const int MaxAgeLimitDays = 3650;

    /// <summary>Gets or sets a value indicating whether scheduled backups are enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the frequency.</summary>
    public BackupFrequency Frequency { get; set; } = BackupFrequency.Weekly;

    /// <summary>Gets or sets the weekdays used by <see cref="BackupFrequency.Weekly"/>.</summary>
    public List<DayOfWeek> Weekdays { get; set; } = [DayOfWeek.Sunday];

    /// <summary>Gets or sets the day of month (1–31) used by <see cref="BackupFrequency.Monthly"/>; clamped to the month length.</summary>
    public int DayOfMonth { get; set; } = 1;

    /// <summary>Gets or sets the local time of day of the run.</summary>
    public TimeOnly TimeOfDay { get; set; } = new(2, 0);

    /// <summary>Gets or sets the time zone identifier; <see langword="null"/> means the server's local time zone.</summary>
    public string? TimeZoneId { get; set; }

    /// <summary>Gets or sets the destination folder; <see langword="null"/> means <c>&lt;data dir&gt;/backups/instance</c>. UNC paths are allowed.</summary>
    public string? DestinationFolder { get; set; }

    /// <summary>Gets or sets a value indicating whether attachment binaries are included.</summary>
    public bool IncludeAttachments { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the instance <c>appsettings.json</c> is included.</summary>
    public bool IncludeInstanceSettings { get; set; }

    /// <summary>Gets or sets a value indicating whether archives are encrypted with the configured backup password.</summary>
    public bool EncryptionEnabled { get; set; }

    /// <summary>Gets or sets how many scheduled backups are kept (0 = unlimited).</summary>
    public int KeepLastScheduled { get; set; } = 7;

    /// <summary>Gets or sets the maximum age in days of scheduled backups (0 = unlimited). The newest one is always kept.</summary>
    public int MaxAgeDays { get; set; } = 90;

    /// <summary>Gets or sets how many automatic safety backups (pre-migration and pre-restore, each) are kept (0 = unlimited).</summary>
    public int KeepLastSafety { get; set; } = 5;

    /// <summary>Gets or sets a value indicating whether an occurrence missed during downtime runs as soon as the service is up again.</summary>
    public bool CatchUpMissedRuns { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether a backup is created automatically before database migrations run.</summary>
    public bool PreMigrationBackupEnabled { get; set; } = true;

    /// <summary>Resolves the configured time zone, falling back to the local time zone when it is unknown.</summary>
    /// <param name="localTimeZone">The local time zone to use when none (or an unknown one) is configured.</param>
    /// <returns>The time zone.</returns>
    public TimeZoneInfo ResolveTimeZone(TimeZoneInfo localTimeZone)
    {
        if (string.IsNullOrWhiteSpace(TimeZoneId))
        {
            return localTimeZone;
        }

        return TimeZoneInfo.TryFindSystemTimeZoneById(TimeZoneId, out var zone) ? zone : localTimeZone;
    }

    /// <summary>Validates the settings.</summary>
    /// <returns>User-safe validation errors; empty when valid.</returns>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (!Enum.IsDefined(Frequency))
        {
            errors.Add("The frequency is invalid.");
        }

        if (Frequency == BackupFrequency.Weekly && (Weekdays is null || Weekdays.Count == 0))
        {
            errors.Add("Select at least one weekday for a weekly schedule.");
        }

        if (Weekdays is not null && Weekdays.Any(d => !Enum.IsDefined(d)))
        {
            errors.Add("A weekday is invalid.");
        }

        if (DayOfMonth is < 1 or > 31)
        {
            errors.Add("The day of month must be between 1 and 31.");
        }

        if (!string.IsNullOrWhiteSpace(TimeZoneId) && !TimeZoneInfo.TryFindSystemTimeZoneById(TimeZoneId, out _))
        {
            errors.Add($"The time zone '{TimeZoneId}' is not known on this server.");
        }

        if (KeepLastScheduled is < 0 or > MaxKeep || KeepLastSafety is < 0 or > MaxKeep)
        {
            errors.Add($"Retention counts must be between 0 and {MaxKeep}.");
        }

        if (MaxAgeDays is < 0 or > MaxAgeLimitDays)
        {
            errors.Add($"The maximum age must be between 0 and {MaxAgeLimitDays} days.");
        }

        if (DestinationFolder is { Length: > 0 } folder && (folder.Length > 400 || folder.IndexOfAny(['"', '<', '>', '|', '*', '?']) >= 0))
        {
            errors.Add("The destination folder is not a valid path.");
        }

        return errors;
    }

    /// <summary>Creates a deep copy.</summary>
    /// <returns>The copy.</returns>
    public BackupSchedule Clone()
    {
        var copy = (BackupSchedule)MemberwiseClone();
        copy.Weekdays = [.. Weekdays ?? []];
        return copy;
    }
}
