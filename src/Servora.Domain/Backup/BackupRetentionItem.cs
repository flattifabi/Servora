namespace Servora.Domain.Backup;

/// <summary>A backup as seen by the retention planner.</summary>
/// <param name="Id">The backup identifier (archive file name).</param>
/// <param name="Kind">The backup kind.</param>
/// <param name="CreatedAt">When the backup was created (UTC).</param>
public sealed record BackupRetentionItem(string Id, BackupKind Kind, DateTimeOffset CreatedAt);
