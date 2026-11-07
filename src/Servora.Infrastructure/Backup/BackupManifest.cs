using Servora.Domain.Backup;

namespace Servora.Infrastructure.Backup;

/// <summary>
/// Self-describing header of a backup archive: who/what/when, the schema (tables, columns, keys, foreign keys) read
/// from the EF Core relational model of the backed-up migration, and the hash of every entry.
/// </summary>
public sealed class BackupManifest
{
    /// <summary>Gets or sets the archive format version.</summary>
    public int FormatVersion { get; set; } = BackupArchiveFormat.FormatVersion;

    /// <summary>Gets or sets the product marker.</summary>
    public string Product { get; set; } = BackupArchiveFormat.Product;

    /// <summary>Gets or sets the Servora version that created the archive.</summary>
    public string AppVersion { get; set; } = string.Empty;

    /// <summary>Gets or sets the database provider key (<c>Sqlite</c>, <c>SqlServer</c>, <c>PostgreSql</c>).</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Gets or sets the EF Core provider name (for example <c>Microsoft.EntityFrameworkCore.Sqlite</c>).</summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>Gets or sets the last applied migration, or <see langword="null"/> when the database had no migration history.</summary>
    public string? Migration { get; set; }

    /// <summary>Gets or sets when the archive was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets who or what created the archive.</summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>Gets or sets the machine that created the archive.</summary>
    public string MachineName { get; set; } = string.Empty;

    /// <summary>Gets or sets the backup kind.</summary>
    public BackupKind Kind { get; set; }

    /// <summary>Gets or sets a value indicating whether attachment binaries are included.</summary>
    public bool IncludesAttachments { get; set; }

    /// <summary>Gets or sets a value indicating whether the instance <c>appsettings.json</c> is included.</summary>
    public bool IncludesInstanceSettings { get; set; }

    /// <summary>Gets or sets the encryption parameters, or <see langword="null"/> for an unencrypted archive.</summary>
    public BackupEncryptionManifest? Encryption { get; set; }

    /// <summary>Gets or sets the tables in insert (foreign key) order.</summary>
    public List<BackupTableManifest> Tables { get; set; } = [];

    /// <summary>Gets or sets every entry of the archive except the manifest and its signature.</summary>
    public List<BackupEntryManifest> Entries { get; set; } = [];

    /// <summary>Gets the total number of rows.</summary>
    public long TotalRows => Tables.Sum(t => t.RowCount);
}
