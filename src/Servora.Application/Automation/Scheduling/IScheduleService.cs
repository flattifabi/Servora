using Servora.Application.Common;

namespace Servora.Application.Automation.Scheduling;

/// <summary>Scheduled jobs page use cases.</summary>
public interface IScheduleService
{
    /// <summary>Lists all scheduled jobs (<c>automation.read</c>).</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The jobs ordered by next run.</returns>
    Task<Result<IReadOnlyList<ScheduleItem>>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Validates a cron expression and previews the next occurrences.</summary>
    /// <param name="cronExpression">The expression.</param>
    /// <param name="timeZoneId">The time zone.</param>
    /// <param name="count">The number of occurrences (1–20).</param>
    /// <returns>The occurrences (UTC) or a validation failure.</returns>
    Result<IReadOnlyList<DateTimeOffset>> Preview(string? cronExpression, string? timeZoneId, int count = 5);

    /// <summary>Runs a scheduled job now (<c>automation.execute</c> for flows, <c>integration.imports.manage</c> for imports).</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="id">The flow or import set.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The run identifier.</returns>
    Task<Result<Guid>> RunNowAsync(ScheduleKind kind, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Enables or disables a schedule (<c>automation.manage</c> / <c>integration.imports.manage</c>).</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="id">The flow or import set.</param>
    /// <param name="enabled">The new state.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> SetEnabledAsync(ScheduleKind kind, Guid id, bool enabled, CancellationToken cancellationToken = default);
}
