namespace Servora.Infrastructure.Backup;

/// <summary>Result of <see cref="BackupJobRunner"/>: the new archive and (in memory only) the password it was encrypted with.</summary>
/// <param name="Entry">The archive.</param>
/// <param name="Password">The encryption password, or <see langword="null"/> for an unencrypted archive. Never log or persist it in clear text.</param>
public sealed record BackupJobResult(BackupCatalogEntry Entry, string? Password)
{
    /// <inheritdoc />
    public override string ToString() => Entry.Id;
}
