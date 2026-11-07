namespace Servora.Infrastructure.Backup;

/// <summary>An archive entry with its integrity information.</summary>
public sealed class BackupEntryManifest
{
    /// <summary>Gets or sets the zero-based entry index (part of the encryption nonce).</summary>
    public int Index { get; set; }

    /// <summary>Gets or sets the entry path inside the archive.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Gets or sets the entry kind (see <see cref="BackupArchiveFormat"/> <c>Kind*</c> constants).</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Gets or sets the length of the stored entry content in bytes.</summary>
    public long Length { get; set; }

    /// <summary>Gets or sets the lower-case hexadecimal SHA-256 of the stored entry content.</summary>
    public string Sha256 { get; set; } = string.Empty;
}
