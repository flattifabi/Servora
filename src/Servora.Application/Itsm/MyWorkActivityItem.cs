using Servora.Domain.Activity;

namespace Servora.Application.Itsm;

/// <summary>A recent activity entry on one of the current user's records.</summary>
/// <param name="Table">The technical table name.</param>
/// <param name="RecordId">The record identifier.</param>
/// <param name="RecordNumber">The record number.</param>
/// <param name="Route">The UI route of the record.</param>
/// <param name="Type">The entry type.</param>
/// <param name="AuthorName">The author display name.</param>
/// <param name="Text">The (shortened) text; for field changes a summary of the changed fields.</param>
/// <param name="CreatedAt">The UTC time of the entry.</param>
public sealed record MyWorkActivityItem(
    string Table,
    Guid RecordId,
    string RecordNumber,
    string Route,
    ActivityType Type,
    string AuthorName,
    string Text,
    DateTimeOffset CreatedAt);
