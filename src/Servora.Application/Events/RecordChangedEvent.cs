namespace Servora.Application.Events;

/// <summary>
/// Raised after a record of a registered table (an entity with <c>[RecordTable]</c>, or a custom table record) was
/// committed. Consumed asynchronously by business rules (async), flows, SLAs, notifications and the activity stream.
/// </summary>
/// <param name="TableName">The technical table name.</param>
/// <param name="RecordId">The record identifier.</param>
/// <param name="ChangeType">The change type.</param>
/// <param name="Changes">The changed fields (all set fields for inserts, empty for deletes).</param>
/// <param name="Snapshot">The field values after the change (before the change for deletes).</param>
/// <param name="ActorUserId">The user that caused the change, if any.</param>
/// <param name="ActorName">The display name of the actor.</param>
/// <param name="OccurredAt">The UTC commit time.</param>
/// <param name="Depth">The cascade depth (events caused by handlers of other events). Used to stop infinite loops.</param>
public sealed record RecordChangedEvent(
    string TableName,
    Guid RecordId,
    RecordChangeType ChangeType,
    IReadOnlyList<RecordFieldChange> Changes,
    IReadOnlyDictionary<string, object?> Snapshot,
    Guid? ActorUserId,
    string ActorName,
    DateTimeOffset OccurredAt,
    int Depth)
{
    /// <summary>Checks whether a field changed.</summary>
    /// <param name="field">The field name (case-insensitive).</param>
    /// <returns><see langword="true"/> when the field is part of <see cref="Changes"/>.</returns>
    public bool HasChanged(string field) => Changes.Any(c => string.Equals(c.Field, field, StringComparison.OrdinalIgnoreCase));
}
