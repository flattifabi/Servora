namespace Servora.Domain.Backup;

/// <summary>Result of <see cref="BackupSchedulePlanner.Evaluate"/>.</summary>
/// <param name="Action">What to do now.</param>
/// <param name="Occurrence">The occurrence (UTC) that is run or skipped; <see langword="null"/> when idle.</param>
/// <param name="NextOccurrence">The next future occurrence (UTC), or <see langword="null"/> when the schedule is disabled.</param>
/// <param name="IsCatchUp">Whether a run is a catch-up of an occurrence missed during downtime.</param>
public sealed record BackupScheduleDecision(BackupScheduleAction Action, DateTimeOffset? Occurrence, DateTimeOffset? NextOccurrence, bool IsCatchUp = false);
