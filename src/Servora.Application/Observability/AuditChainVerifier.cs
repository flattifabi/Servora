using Servora.Domain.Observability;

namespace Servora.Application.Observability;

/// <summary>
/// Incremental verifier of the audit hash chain. Feed the events in chain order (ascending
/// <see cref="AuditEvent.Sequence"/>); every event's content hash is recomputed and its link to the predecessor checked.
/// Detects modified events (hash mismatch), deleted / inserted / reordered events (broken link, sequence gap) and
/// removal of the oldest events beyond the retention anchor (missing start). Memory use is constant.
/// </summary>
/// <param name="anchorHash">The hash of the newest pruned event, or <see langword="null"/> when nothing was pruned.</param>
/// <param name="anchorSequence">The sequence of the newest pruned event (0 when nothing was pruned).</param>
/// <param name="maxListedIssues">The maximum number of issues kept in <see cref="Issues"/>.</param>
public sealed class AuditChainVerifier(string? anchorHash, long anchorSequence, int maxListedIssues = 50)
{
    private readonly List<AuditChainIssue> _issues = [];
    private string? _lastHash;
    private long _lastSequence;

    /// <summary>Gets the number of verified events.</summary>
    public long CheckedCount { get; private set; }

    /// <summary>Gets the total number of issues found.</summary>
    public long IssueCount { get; private set; }

    /// <summary>Gets the first issues found (bounded).</summary>
    public IReadOnlyList<AuditChainIssue> Issues => _issues;

    /// <summary>Gets the sequence of the first verified event.</summary>
    public long FirstSequence { get; private set; }

    /// <summary>Gets the sequence of the last verified event.</summary>
    public long LastSequence => _lastSequence;

    /// <summary>Gets the stored hash of the last verified event (chain head).</summary>
    public string? HeadHash => _lastHash;

    /// <summary>Gets a value indicating whether no issue was found so far.</summary>
    public bool IsIntact => IssueCount == 0;

    /// <summary>Verifies the next event of the chain.</summary>
    /// <param name="auditEvent">The event.</param>
    public void Add(AuditEvent auditEvent)
    {
        if (CheckedCount == 0)
        {
            FirstSequence = auditEvent.Sequence;
            if (!string.Equals(auditEvent.PreviousHash, anchorHash, StringComparison.Ordinal))
            {
                Report(AuditChainIssueKind.MissingStart, auditEvent, anchorHash is null
                    ? "The oldest event links to a predecessor that no longer exists: events at the start of the log were deleted."
                    : "The oldest retained event does not link to the retention anchor: events were deleted outside of retention.");
            }
            else if (auditEvent.Sequence != anchorSequence + 1)
            {
                Report(AuditChainIssueKind.SequenceGap, auditEvent, $"The chain should start at sequence {anchorSequence + 1} but starts at {auditEvent.Sequence}.");
            }
        }
        else if (!string.Equals(auditEvent.PreviousHash, _lastHash, StringComparison.Ordinal))
        {
            var missing = auditEvent.Sequence - _lastSequence - 1;
            Report(AuditChainIssueKind.BrokenLink, auditEvent, missing > 0
                ? $"The link to the previous event is broken; {missing} event(s) between sequence {_lastSequence} and {auditEvent.Sequence} are missing."
                : "The link to the previous event is broken: events were inserted, deleted or reordered.");
        }
        else if (auditEvent.Sequence != _lastSequence + 1)
        {
            Report(AuditChainIssueKind.SequenceGap, auditEvent, $"Expected sequence {_lastSequence + 1} but found {auditEvent.Sequence}.");
        }

        if (string.IsNullOrEmpty(auditEvent.Hash))
        {
            Report(AuditChainIssueKind.MissingHash, auditEvent, "The event has no hash.");
        }
        else if (!string.Equals(AuditHasher.ComputeHash(auditEvent), auditEvent.Hash, StringComparison.Ordinal))
        {
            Report(AuditChainIssueKind.HashMismatch, auditEvent, "The content of the event was modified after it was written (hash mismatch).");
        }

        _lastHash = auditEvent.Hash;
        _lastSequence = auditEvent.Sequence;
        CheckedCount++;
    }

    private void Report(AuditChainIssueKind kind, AuditEvent auditEvent, string message)
    {
        IssueCount++;
        if (_issues.Count < maxListedIssues)
        {
            _issues.Add(new AuditChainIssue(kind, auditEvent.Id, auditEvent.Sequence, auditEvent.OccurredAt, message));
        }
    }
}
