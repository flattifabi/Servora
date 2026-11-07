using Servora.Domain.Observability;

namespace Servora.Infrastructure.Backup;

/// <summary>
/// An audit event of a backup operation that ran before the audit log was usable (restore at startup, pre-migration
/// backup, command line). The backup scheduler writes it to the audit log once the application is running.
/// </summary>
public sealed class PendingBackupAudit
{
    /// <summary>Gets or sets the audit action, for example <c>backup.restored</c>.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Gets or sets the outcome.</summary>
    public AuditOutcome Outcome { get; set; } = AuditOutcome.Success;

    /// <summary>Gets or sets the affected backup.</summary>
    public string? BackupId { get; set; }

    /// <summary>Gets or sets the user-safe message.</summary>
    public string? Message { get; set; }

    /// <summary>Gets or sets who triggered the operation.</summary>
    public string Actor { get; set; } = "System";

    /// <summary>Gets or sets when it happened.</summary>
    public DateTimeOffset At { get; set; }
}
