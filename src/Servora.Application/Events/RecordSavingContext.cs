namespace Servora.Application.Events;

/// <summary>Context passed to <see cref="IRecordSavingHook"/> for a pending change.</summary>
/// <param name="tableName">The technical table name.</param>
/// <param name="entity">The tracked entity instance (typed entity or custom record).</param>
/// <param name="changeType">The pending change type.</param>
/// <param name="changes">The pending field changes.</param>
public sealed class RecordSavingContext(string tableName, object entity, RecordChangeType changeType, IReadOnlyList<RecordFieldChange> changes)
{
    /// <summary>Gets the technical table name.</summary>
    public string TableName { get; } = tableName;

    /// <summary>Gets the tracked entity. Modifications are persisted with the save.</summary>
    public object Entity { get; } = entity;

    /// <summary>Gets the pending change type.</summary>
    public RecordChangeType ChangeType { get; } = changeType;

    /// <summary>Gets the pending field changes.</summary>
    public IReadOnlyList<RecordFieldChange> Changes { get; } = changes;

    /// <summary>Gets the abort message when a hook aborted the save.</summary>
    public string? AbortMessage { get; private set; }

    /// <summary>Gets a value indicating whether a hook aborted the save.</summary>
    public bool IsAborted => AbortMessage is not null;

    /// <summary>Aborts the whole save operation with a user-safe message.</summary>
    /// <param name="message">The message shown to the user.</param>
    public void Abort(string message) => AbortMessage = message;

    /// <summary>Checks whether a field is part of the pending changes.</summary>
    /// <param name="field">The field name (case-insensitive).</param>
    /// <returns><see langword="true"/> when the field changes.</returns>
    public bool HasChanged(string field) => Changes.Any(c => string.Equals(c.Field, field, StringComparison.OrdinalIgnoreCase));
}
