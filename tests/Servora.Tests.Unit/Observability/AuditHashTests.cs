using Servora.Domain.Observability;
using Servora.Infrastructure.Observability;

namespace Servora.Tests.Unit.Observability;

/// <summary>Tests for the audit hash chain.</summary>
public sealed class AuditHashTests
{
    [Fact]
    public void ComputeHash_ChangesWhenContentOrChainLinkChanges()
    {
        var auditEvent = new AuditEvent { OccurredAt = DateTimeOffset.UnixEpoch, Category = "auth", Action = "login.success", ActorName = "admin" };
        var original = AuditService.ComputeHash(auditEvent);

        auditEvent.Message = "tampered";
        var tampered = AuditService.ComputeHash(auditEvent);

        auditEvent.Message = null;
        auditEvent.PreviousHash = "abc";
        var relinked = AuditService.ComputeHash(auditEvent);

        Assert.Equal(64, original.Length);
        Assert.NotEqual(original, tampered);
        Assert.NotEqual(original, relinked);
    }
}
