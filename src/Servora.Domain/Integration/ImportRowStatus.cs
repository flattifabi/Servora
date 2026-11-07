namespace Servora.Domain.Integration;

/// <summary>Outcome of one staged import row.</summary>
public enum ImportRowStatus
{
    /// <summary>Staged, not yet transformed.</summary>
    Pending = 0,

    /// <summary>A new target record was created.</summary>
    Inserted = 1,

    /// <summary>An existing target record (matched by the coalesce field) was updated.</summary>
    Updated = 2,

    /// <summary>The row was skipped (for example no mapped values).</summary>
    Skipped = 3,

    /// <summary>The row could not be written.</summary>
    Error = 4,
}
