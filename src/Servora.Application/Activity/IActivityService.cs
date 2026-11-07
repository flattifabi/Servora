using Servora.Application.Common;
using Servora.Domain.Activity;

namespace Servora.Application.Activity;

/// <summary>Reads and writes the activity stream (comments, work notes, field changes, system events) of any record.</summary>
public interface IActivityService
{
    /// <summary>Gets the activity stream of a record, newest first.</summary>
    /// <param name="tableName">The technical table name.</param>
    /// <param name="recordId">The record identifier.</param>
    /// <param name="includeWorkNotes">Whether internal work notes and field changes are included.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The entries.</returns>
    Task<IReadOnlyList<ActivityEntry>> GetAsync(string tableName, Guid recordId, bool includeWorkNotes, CancellationToken cancellationToken = default);

    /// <summary>Adds a comment or work note authored by the current actor.</summary>
    /// <param name="tableName">The technical table name.</param>
    /// <param name="recordId">The record identifier.</param>
    /// <param name="type">Either <see cref="ActivityType.Comment"/> or <see cref="ActivityType.WorkNote"/>.</param>
    /// <param name="text">The text (max 20,000 characters).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created entry.</returns>
    Task<Result<ActivityEntry>> AddAsync(string tableName, Guid recordId, ActivityType type, string text, CancellationToken cancellationToken = default);

    /// <summary>Adds a system entry (used by engines: SLA, approvals, flows, notifications).</summary>
    /// <param name="tableName">The technical table name.</param>
    /// <param name="recordId">The record identifier.</param>
    /// <param name="text">The message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the entry is stored.</returns>
    Task AddSystemAsync(string tableName, Guid recordId, string text, CancellationToken cancellationToken = default);
}
