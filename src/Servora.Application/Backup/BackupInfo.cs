using Servora.Domain.Backup;

namespace Servora.Application.Backup;

/// <summary>A backup archive of the catalog.</summary>
/// <param name="Id">The identifier (archive file name).</param>
/// <param name="Kind">The effective kind (uploaded archives are <see cref="BackupKind.Uploaded"/>).</param>
/// <param name="OriginalKind">The kind recorded in the archive manifest.</param>
/// <param name="CreatedAt">When the backup was created (UTC).</param>
/// <param name="CreatedBy">Who or what created the backup.</param>
/// <param name="AppVersion">The Servora version that created the backup.</param>
/// <param name="Provider">The database provider key.</param>
/// <param name="Migration">The last applied migration at backup time, or <see langword="null"/> when the database had no migrations.</param>
/// <param name="SizeBytes">The archive size in bytes.</param>
/// <param name="IsEncrypted">Whether the archive is password encrypted.</param>
/// <param name="IncludesAttachments">Whether attachment binaries are included.</param>
/// <param name="TableCount">The number of tables.</param>
/// <param name="RowCount">The total number of rows.</param>
/// <param name="Integrity">The integrity state known to the catalog.</param>
/// <param name="Message">A problem description when the archive is unreadable.</param>
public sealed record BackupInfo(
    string Id,
    BackupKind Kind,
    BackupKind OriginalKind,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    string AppVersion,
    string Provider,
    string? Migration,
    long SizeBytes,
    bool IsEncrypted,
    bool IncludesAttachments,
    int TableCount,
    long RowCount,
    BackupIntegrity Integrity,
    string? Message);
