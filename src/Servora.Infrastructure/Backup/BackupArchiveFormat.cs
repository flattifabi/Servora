namespace Servora.Infrastructure.Backup;

/// <summary>
/// Layout of a <c>.svbak</c> archive (a zip file):
/// <list type="bullet">
/// <item><c>manifest.json</c> – <see cref="BackupManifest"/> with schema metadata and the SHA-256 of every other entry (written last);</item>
/// <item><c>manifest.sig</c> – HMAC-SHA256 of the manifest bytes, only in encrypted archives;</item>
/// <item><c>db/NNNN.bin</c> – one logical dump per table (<see cref="TableDumpWriter"/>);</item>
/// <item><c>keys/…</c> – the Data Protection key ring; <c>attachments/…</c> – attachment binaries (optional);
/// <c>data/…</c> – other data directory files (branding, uploads, optionally the instance <c>appsettings.json</c>).</item>
/// </list>
/// Entry hashes are computed over the stored entry content (after encryption), so tampering is detected without the password.
/// </summary>
public static class BackupArchiveFormat
{
    /// <summary>The current archive format version.</summary>
    public const int FormatVersion = 1;

    /// <summary>The product marker written to every manifest.</summary>
    public const string Product = "Servora";

    /// <summary>The manifest entry name.</summary>
    public const string ManifestEntry = "manifest.json";

    /// <summary>The manifest signature entry name (encrypted archives only).</summary>
    public const string SignatureEntry = "manifest.sig";

    /// <summary>Entry name prefix of table dumps.</summary>
    public const string DatabasePrefix = "db/";

    /// <summary>Entry name prefix of the Data Protection key ring.</summary>
    public const string KeysPrefix = "keys/";

    /// <summary>Entry name prefix of attachment binaries.</summary>
    public const string AttachmentsPrefix = "attachments/";

    /// <summary>Entry name prefix of other data directory files.</summary>
    public const string DataPrefix = "data/";

    /// <summary>Entry kind of a table dump.</summary>
    public const string KindTable = "table";

    /// <summary>Entry kind of a key ring file.</summary>
    public const string KindKey = "key";

    /// <summary>Entry kind of an attachment binary.</summary>
    public const string KindAttachment = "attachment";

    /// <summary>Entry kind of another data directory file.</summary>
    public const string KindFile = "file";

    /// <summary>The maximum accepted manifest size (protects against hostile uploads).</summary>
    public const long MaxManifestBytes = 32L * 1024 * 1024;
}
