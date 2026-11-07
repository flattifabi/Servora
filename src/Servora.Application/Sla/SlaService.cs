using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Tables;
using Servora.Domain.Sla;

namespace Servora.Application.Sla;

/// <summary>Default <see cref="ISlaService"/>: SLA timers of a record, visible to actors who may read the record's table.</summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="access">The table access service.</param>
/// <param name="clock">The clock.</param>
public sealed class SlaService(IAppDbContextFactory dbFactory, ITableAccessService access, IClock clock) : ISlaService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<TaskSlaInfo>> GetForRecordAsync(string tableName, Guid recordId, CancellationToken cancellationToken = default)
    {
        if (!await access.IsAllowedAsync(tableName, TableOperation.Read, cancellationToken))
        {
            return [];
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var timers = await db.Set<TaskSla>().AsNoTracking()
            .Where(t => t.TableName == tableName && t.RecordId == recordId)
            .OrderByDescending(t => t.StartedAt)
            .Take(50)
            .ToListAsync(cancellationToken);
        if (timers.Count == 0)
        {
            return [];
        }

        var ids = timers.Select(t => t.SlaDefinitionId).Distinct().ToList();
        var definitions = await db.Set<SlaDefinition>().AsNoTracking().Where(d => ids.Contains(d.Id)).ToDictionaryAsync(d => d.Id, cancellationToken);
        var now = clock.UtcNow;
        return timers.Select(t =>
        {
            var definition = definitions.GetValueOrDefault(t.SlaDefinitionId);
            var schedule = definition is null ? SlaSchedule.TwentyFourSeven : SlaSchedule.From(definition);
            return new TaskSlaInfo(
                definition?.Name ?? "SLA",
                SlaTimer.StageName(t),
                t.StartedAt,
                t.DueAt,
                SlaTimer.PercentElapsed(t, schedule, definition?.TargetMinutes ?? 0, now),
                t.HasBreached);
        }).ToList();
    }

    /// <summary>The maximum number of record identifiers evaluated by <see cref="GetBreachedRecordIdsAsync"/>.</summary>
    public const int MaxBulkRecords = 1000;

    /// <inheritdoc />
    public async Task<IReadOnlySet<Guid>> GetBreachedRecordIdsAsync(string tableName, IReadOnlyCollection<Guid> recordIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recordIds);
        if (recordIds.Count == 0 || !await access.IsAllowedAsync(tableName, TableOperation.Read, cancellationToken))
        {
            return new HashSet<Guid>();
        }

        var ids = recordIds.Distinct().Take(MaxBulkRecords).ToList();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var breached = await db.Set<TaskSla>().AsNoTracking()
            .Where(t => t.TableName == tableName && t.HasBreached && ids.Contains(t.RecordId))
            .Select(t => t.RecordId)
            .Distinct()
            .ToListAsync(cancellationToken);
        return breached.ToHashSet();
    }
}
