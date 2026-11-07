namespace Servora.Domain.Backup;

/// <summary>
/// Selects backups that retention removes:
/// <list type="bullet">
/// <item>scheduled backups beyond the newest <see cref="BackupSchedule.KeepLastScheduled"/> and those older than
/// <see cref="BackupSchedule.MaxAgeDays"/> (the newest scheduled backup is always kept);</item>
/// <item>pre-migration and pre-restore safety backups beyond the newest <see cref="BackupSchedule.KeepLastSafety"/> of each kind;</item>
/// <item>manual and uploaded backups are never removed automatically.</item>
/// </list>
/// </summary>
public static class BackupRetentionPlanner
{
    /// <summary>Returns the backups to delete.</summary>
    /// <param name="items">All known backups.</param>
    /// <param name="schedule">The retention settings.</param>
    /// <param name="nowUtc">The current instant.</param>
    /// <returns>The backups to delete, oldest first.</returns>
    public static IReadOnlyList<BackupRetentionItem> SelectForDeletion(IEnumerable<BackupRetentionItem> items, BackupSchedule schedule, DateTimeOffset nowUtc)
    {
        var all = items.ToList();
        var delete = new HashSet<BackupRetentionItem>();

        var scheduled = all.Where(i => i.Kind == BackupKind.Scheduled).OrderByDescending(i => i.CreatedAt).ToList();
        for (var index = 0; index < scheduled.Count; index++)
        {
            var item = scheduled[index];
            if (index == 0)
            {
                continue;
            }

            var beyondCount = schedule.KeepLastScheduled > 0 && index >= schedule.KeepLastScheduled;
            var tooOld = schedule.MaxAgeDays > 0 && nowUtc - item.CreatedAt > TimeSpan.FromDays(schedule.MaxAgeDays);
            if (beyondCount || tooOld)
            {
                delete.Add(item);
            }
        }

        if (schedule.KeepLastSafety > 0)
        {
            foreach (var kind in new[] { BackupKind.PreMigration, BackupKind.PreRestore })
            {
                foreach (var item in all.Where(i => i.Kind == kind).OrderByDescending(i => i.CreatedAt).Skip(schedule.KeepLastSafety))
                {
                    delete.Add(item);
                }
            }
        }

        return delete.OrderBy(i => i.CreatedAt).ToList();
    }
}
