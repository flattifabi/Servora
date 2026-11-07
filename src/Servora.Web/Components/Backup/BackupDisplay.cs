using System.Globalization;
using Servora.Application.Backup;
using Servora.Domain.Backup;

namespace Servora.Web.Components.Backup;

/// <summary>Presentation helpers (labels, tones, icons, sizes) shared by the backup components.</summary>
public static class BackupDisplay
{
    /// <summary>Formats a byte count.</summary>
    /// <param name="bytes">The size in bytes.</param>
    /// <returns>The formatted size, for example <c>12.4 MB</c>.</returns>
    public static string Size(long bytes) => bytes switch
    {
        >= 1L << 40 => (bytes / (double)(1L << 40)).ToString("0.0", CultureInfo.InvariantCulture) + " TB",
        >= 1L << 30 => (bytes / (double)(1L << 30)).ToString("0.0", CultureInfo.InvariantCulture) + " GB",
        >= 1L << 20 => (bytes / (double)(1L << 20)).ToString("0.0", CultureInfo.InvariantCulture) + " MB",
        >= 1L << 10 => (bytes / 1024d).ToString("0.0", CultureInfo.InvariantCulture) + " KB",
        _ => bytes.ToString(CultureInfo.InvariantCulture) + " B",
    };

    /// <summary>Returns the label of a backup kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The label.</returns>
    public static string KindLabel(BackupKind kind) => kind switch
    {
        BackupKind.Scheduled => "Scheduled",
        BackupKind.Manual => "Manual",
        BackupKind.PreMigration => "Pre-migration",
        BackupKind.PreRestore => "Pre-restore",
        BackupKind.Uploaded => "Uploaded",
        _ => kind.ToString(),
    };

    /// <summary>Returns the pill tone of a backup kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The tone.</returns>
    public static string KindTone(BackupKind kind) => kind switch
    {
        BackupKind.Scheduled => "primary",
        BackupKind.Manual => "info",
        BackupKind.PreMigration => "purple",
        BackupKind.PreRestore => "warning",
        BackupKind.Uploaded => "accent",
        _ => "neutral",
    };

    /// <summary>Returns the icon of a backup kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The icon name.</returns>
    public static string KindIcon(BackupKind kind) => kind switch
    {
        BackupKind.Scheduled => "calendar-clock",
        BackupKind.Manual => "save",
        BackupKind.PreMigration => "layers",
        BackupKind.PreRestore => "shield-check",
        BackupKind.Uploaded => "upload",
        _ => "hard-drive",
    };

    /// <summary>Returns the label and tone of an integrity state.</summary>
    /// <param name="integrity">The integrity state.</param>
    /// <returns>The label and tone.</returns>
    public static (string Label, string Tone) Integrity(BackupIntegrity integrity) => integrity switch
    {
        BackupIntegrity.Valid => ("Verified", "success"),
        BackupIntegrity.Invalid => ("Invalid", "danger"),
        _ => ("Not verified", "neutral"),
    };

    /// <summary>Describes when a schedule runs, for example <c>Weekly on Sun, Wed at 02:00</c>.</summary>
    /// <param name="schedule">The schedule.</param>
    /// <returns>The description.</returns>
    public static string DescribeSchedule(BackupSchedule schedule)
    {
        var time = schedule.TimeOfDay.ToString("HH:mm", CultureInfo.InvariantCulture);
        return schedule.Frequency switch
        {
            BackupFrequency.Daily => $"Daily at {time}",
            BackupFrequency.Weekly => $"Weekly on {string.Join(", ", schedule.Weekdays.OrderBy(d => ((int)d + 6) % 7).Select(d => CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedDayName(d)))} at {time}",
            BackupFrequency.Monthly => schedule.DayOfMonth >= 29 ? $"Monthly on day {schedule.DayOfMonth} (or the last day) at {time}" : $"Monthly on day {schedule.DayOfMonth} at {time}",
            _ => time,
        };
    }

    /// <summary>Describes the retention settings.</summary>
    /// <param name="schedule">The schedule.</param>
    /// <returns>The description.</returns>
    public static string DescribeRetention(BackupSchedule schedule)
    {
        var count = schedule.KeepLastScheduled > 0 ? $"last {schedule.KeepLastScheduled} scheduled" : "all scheduled";
        var age = schedule.MaxAgeDays > 0 ? $", max. {schedule.MaxAgeDays} days" : string.Empty;
        var safety = schedule.KeepLastSafety > 0 ? $"; {schedule.KeepLastSafety} safety backups each" : "; all safety backups";
        return count + age + safety;
    }

    /// <summary>Formats an instant in a time zone, for example <c>Sun, 08 Nov 2026 02:00</c>.</summary>
    /// <param name="value">The instant.</param>
    /// <param name="timeZone">The time zone.</param>
    /// <returns>The formatted value.</returns>
    public static string InZone(DateTimeOffset value, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTime(value, timeZone).ToString("ddd, dd MMM yyyy HH:mm", CultureInfo.InvariantCulture);
}
