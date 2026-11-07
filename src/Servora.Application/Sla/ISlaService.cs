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
}
