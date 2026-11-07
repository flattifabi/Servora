namespace Servora.Application.Observability;

/// <summary>Result of an audit hash-chain verification.</summary>
/// <param name="IsIntact">Whether no integrity problem was found.</param>
/// <param name="CheckedCount">The number of verified events.</param>
/// <param name="IssueCount">The total number of problems (only the first <see cref="Issues"/> are listed).</param>
/// <param name="Issues">The first problems found (bounded).</param>
/// <param name="FirstSequence">The sequence number of the oldest verified event.</param>
/// <param name="LastSequence">The sequence number of the newest verified event.</param>
/// <param name="HeadHash">The hash of the newest event (record it externally to detect tail truncation).</param>
/// <param name="VerifiedAt">The UTC time of the verification.</param>
/// <param name="DurationMs">The verification duration in milliseconds.</param>
/// <param name="VerifiedBy">The display name of the actor who ran the verification.</param>
public sealed record AuditChainVerification(
    bool IsIntact,
    long CheckedCount,
    long IssueCount,
    IReadOnlyList<AuditChainIssue> Issues,
    long FirstSequence,
    long LastSequence,
    string? HeadHash,
    DateTimeOffset VerifiedAt,
    long DurationMs,
    string VerifiedBy);
