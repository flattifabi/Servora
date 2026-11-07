using Servora.Application.Backup;
using Servora.Domain.Backup;

namespace Servora.Infrastructure.Backup;

/// <summary>An archive file found in a backup folder.</summary>
/// <param name="Id">The identifier (file name).</param>
/// <param name="Path">The full path.</param>
/// <param name="SizeBytes">The file size.</param>
/// <param name="LastWriteUtc">The last write time of the file.</param>
/// <param name="Manifest">The manifest, or <see langword="null"/> when the archive is unreadable.</param>
/// <param name="Integrity">The integrity state.</param>
/// <param name="Error">The reason the archive is unreadable or invalid.</param>
public sealed record BackupCatalogEntry(
    string Id,
    string Path,
    long SizeBytes,
    DateTime LastWriteUtc,
    BackupManifest? Manifest,
    BackupIntegrity Integrity,
    string? Error)
{
    /// <summary>Gets the effective kind.</summary>
    public BackupKind Kind => BackupFileNames.EffectiveKind(Id, Manifest?.Kind ?? BackupKind.Manual);

    /// <summary>Gets the creation time (manifest, or the file time for unreadable archives).</summary>
    public DateTimeOffset CreatedAt => Manifest?.CreatedAt ?? new DateTimeOffset(LastWriteUtc, TimeSpan.Zero);

    /// <summary>Converts the entry to the application DTO.</summary>
    /// <returns>The DTO.</returns>
    public BackupInfo ToInfo() => new(
        Id,
        Kind,
        Manifest?.Kind ?? Kind,
        CreatedAt,
        Manifest?.CreatedBy ?? "unknown",
        Manifest?.AppVersion ?? "unknown",
        Manifest?.Provider ?? "unknown",
        Manifest?.Migration,
        SizeBytes,
        Manifest?.Encryption is not null,
        Manifest?.IncludesAttachments ?? false,
        Manifest?.Tables.Count ?? 0,
        Manifest?.TotalRows ?? 0,
        Integrity,
        Error);
}
