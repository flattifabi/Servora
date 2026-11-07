namespace Servora.Infrastructure.Backup;

/// <summary>A restore request taken for processing, with its passwords unprotected (in memory only).</summary>
/// <param name="Request">The request.</param>
/// <param name="Password">The archive password.</param>
/// <param name="SafetyPassword">The password of the safety backup.</param>
public sealed record RestoreTicket(RestoreRequest Request, string? Password, string? SafetyPassword)
{
    /// <inheritdoc />
    public override string ToString() => Request.BackupId;
}
