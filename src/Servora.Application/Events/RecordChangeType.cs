namespace Servora.Application.Events;

/// <summary>Kind of change applied to a record.</summary>
public enum RecordChangeType
{
    /// <summary>The record was inserted.</summary>
    Created = 0,

    /// <summary>The record was updated.</summary>
    Updated = 1,

    /// <summary>The record was deleted.</summary>
    Deleted = 2,
}
