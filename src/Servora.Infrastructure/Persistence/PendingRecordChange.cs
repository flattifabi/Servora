using Microsoft.EntityFrameworkCore.ChangeTracking;
using Servora.Application.Events;
using Servora.Domain.Common;

namespace Servora.Infrastructure.Persistence;

/// <summary>A record change detected by <see cref="ServoraDbContext"/> while saving, before it is committed.</summary>
/// <param name="tableName">The technical table name.</param>
/// <param name="entity">The tracked entity.</param>
/// <param name="entry">The change tracker entry (used to capture the snapshot before the commit).</param>
/// <param name="changeType">The change type.</param>
/// <param name="changes">The field changes.</param>
internal sealed class PendingRecordChange(string tableName, Entity entity, EntityEntry entry, RecordChangeType changeType, IReadOnlyList<RecordFieldChange> changes)
{
    /// <summary>Gets the technical table name.</summary>
    public string TableName { get; } = tableName;

    /// <summary>Gets the tracked entity.</summary>
    public Entity Entity { get; } = entity;

    /// <summary>Gets the change type.</summary>
    public RecordChangeType ChangeType { get; } = changeType;

    /// <summary>Gets the field changes.</summary>
    public IReadOnlyList<RecordFieldChange> Changes { get; } = changes;

    /// <summary>Gets the field values captured before the commit (deleted entries are detached afterwards).</summary>
    public IReadOnlyDictionary<string, object?> Snapshot { get; } = RecordSnapshotBuilder.Build(entry);
}
