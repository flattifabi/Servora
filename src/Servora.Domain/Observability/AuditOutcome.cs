namespace Servora.Domain.Observability;

/// <summary>Outcome of an audited operation.</summary>
public enum AuditOutcome
{
    /// <summary>The operation succeeded.</summary>
    Success = 0,

    /// <summary>The operation failed (validation, technical error).</summary>
    Failure = 1,

    /// <summary>The operation was denied by authorization.</summary>
    Denied = 2,
}
