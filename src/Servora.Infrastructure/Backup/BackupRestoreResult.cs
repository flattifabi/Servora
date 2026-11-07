namespace Servora.Infrastructure.Backup;

/// <summary>Result of a completed restore.</summary>
/// <param name="Rows">The number of restored rows.</param>
/// <param name="Tables">The number of restored tables.</param>
/// <param name="Files">The number of restored files (key ring, attachments, data files).</param>
/// <param name="Migration">The migration the database was set to (before forward migration at startup).</param>
public sealed record BackupRestoreResult(long Rows, int Tables, int Files, string? Migration);
