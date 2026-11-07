namespace Servora.Domain.Backup;

/// <summary>How often the scheduled backup runs.</summary>
public enum BackupFrequency
{
    /// <summary>Every day at the configured time.</summary>
    Daily = 0,

    /// <summary>On the selected weekdays at the configured time.</summary>
    Weekly = 1,

    /// <summary>Once a month on the configured day (clamped to the last day of shorter months).</summary>
    Monthly = 2,
}
