namespace Servora.Application.Sla;

/// <summary>Reads SLA timers of records. Implemented by the Platform module (T-07); displayed by task forms (T-04, T-05).</summary>
public interface ISlaService
{
    /// <summary>Gets the SLA timers of a record.</summary>
    /// <param name="tableName">The technical table name.</param>
    /// <param name="recordId">The record identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The timers.</returns>
    Task<IReadOnlyList<TaskSlaInfo>> GetForRecordAsync(string tableName, Guid recordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the subset of the given records that have at least one breached SLA (bulk lookup for list KPIs).
    /// The default implementation queries record by record; implementations should override it with a single query.
    /// </summary>
    /// <param name="tableName">The technical table name.</param>
    /// <param name="recordIds">The record identifiers (callers should keep the set bounded, e.g. ≤ 500).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The identifiers of records with a breached SLA.</returns>
    async Task<IReadOnlySet<Guid>> GetBreachedRecordIdsAsync(string tableName, IReadOnlyCollection<Guid> recordIds, CancellationToken cancellationToken = default)
    {
        var breached = new HashSet<Guid>();
        foreach (var id in recordIds)
        {
            var timers = await GetForRecordAsync(tableName, id, cancellationToken);
            if (timers.Any(t => t.Breached))
            {
                breached.Add(id);
            }
        }

        return breached;
    }
}
