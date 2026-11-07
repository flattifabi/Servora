namespace Servora.Application.Observability;

/// <summary>Kind of integrity problem found by the audit hash-chain verification.</summary>
public enum AuditChainIssueKind
{
    /// <summary>The stored hash does not match the event content (the event was modified).</summary>
    HashMismatch = 0,

    /// <summary>The event does not link to the hash of its predecessor (events were deleted, inserted or reordered).</summary>
    BrokenLink = 1,

    /// <summary>The chain does not start at the beginning or at the retention anchor (oldest events were deleted).</summary>
    MissingStart = 2,

    /// <summary>The sequence numbers are not contiguous (events were deleted).</summary>
    SequenceGap = 3,

    /// <summary>The event has no hash at all.</summary>
    MissingHash = 4,
}
