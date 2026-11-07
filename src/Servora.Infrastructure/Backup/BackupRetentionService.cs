using Microsoft.Extensions.Logging;
using Servora.Domain.Backup;

namespace Servora.Infrastructure.Backup;

/// <summary>Applies the retention rules (<see cref="BackupRetentionPlanner"/>) to the archives of the catalog.</summary>
/// <param name="catalog">The catalog.</param>
/// <param name="time">The time provider.</param>
/// <param name="logger">The logger.</param>
public sealed class BackupRetentionService(BackupCatalog catalog, TimeProvider time, ILogger<BackupRetentionService> logger)
{
    /// <summary>Deletes the archives that fall out of retention.</summary>
    /// <param name="schedule">The retention settings.</param>
    /// <returns>The identifiers of the deleted archives.</returns>
    public IReadOnlyList<string> Apply(BackupSchedule schedule)
    {
        var entries = catalog.List().Where(e => e.Manifest is not null).ToList();
        var candidates = BackupRetentionPlanner.SelectForDeletion(
            entries.Select(e => new BackupRetentionItem(e.Id, e.Kind, e.CreatedAt)),
            schedule,
            time.GetUtcNow());

        var deleted = new List<string>();
        foreach (var candidate in candidates)
        {
            var entry = entries.First(e => e.Id == candidate.Id);
            try
            {
                File.Delete(entry.Path);
                catalog.Forget(entry.Path);
                deleted.Add(entry.Id);
                logger.LogInformation("Retention removed backup {Backup} ({Kind}, created {CreatedAt:u})", entry.Id, entry.Kind, entry.CreatedAt);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Retention could not remove backup {Backup}", entry.Id);
            }
        }

        return deleted;
    }
}
