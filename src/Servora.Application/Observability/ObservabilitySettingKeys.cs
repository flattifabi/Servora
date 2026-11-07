namespace Servora.Application.Observability;

/// <summary>Keys of the system settings (<c>sys_properties</c>) owned by the Observability module.</summary>
public static class ObservabilitySettingKeys
{
    /// <summary>Settings category shown in the system settings UI.</summary>
    public const string Category = "Observability";

    /// <summary>Runtime log levels (debug switch) as JSON.</summary>
    public const string LogLevels = "observability.logging.levels";

    /// <summary>Log file retention in days.</summary>
    public const string LogRetentionDays = "observability.logging.retention_days";

    /// <summary>Maximum log file size in MB.</summary>
    public const string LogMaxFileSizeMb = "observability.logging.max_file_size_mb";

    /// <summary>Audit log retention in days (0 = keep forever).</summary>
    public const string AuditRetentionDays = "observability.audit.retention_days";

    /// <summary>Hash of the newest pruned audit event; the first retained event must link to it.</summary>
    public const string AuditChainAnchor = "observability.audit.chain_anchor";

    /// <summary>Sequence number of the newest pruned audit event.</summary>
    public const string AuditChainAnchorSequence = "observability.audit.chain_anchor_sequence";

    /// <summary>Result of the most recent hash-chain verification as JSON.</summary>
    public const string AuditLastVerification = "observability.audit.last_verification";
}
