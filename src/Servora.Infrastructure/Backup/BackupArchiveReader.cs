using System.IO.Compression;
using System.Text.Json;
using Servora.Application.Backup;

namespace Servora.Infrastructure.Backup;

/// <summary>
/// Reads a <c>.svbak</c> archive: validates the manifest, verifies entry hashes and (for encrypted archives) the
/// password and manifest signature, and opens entries as plain streams.
/// </summary>
public sealed class BackupArchiveReader : IDisposable
{
    private readonly ZipArchive _zip;
    private readonly byte[] _manifestBytes;
    private BackupCipher? _cipher;

    private BackupArchiveReader(ZipArchive zip, BackupManifest manifest, byte[] manifestBytes)
    {
        _zip = zip;
        Manifest = manifest;
        _manifestBytes = manifestBytes;
    }

    /// <summary>Gets the manifest.</summary>
    public BackupManifest Manifest { get; }

    /// <summary>Gets a value indicating whether the archive is encrypted.</summary>
    public bool IsEncrypted => Manifest.Encryption is not null;

    /// <summary>Gets a value indicating whether the archive was unlocked with the correct password (or is not encrypted).</summary>
    public bool IsUnlocked => !IsEncrypted || _cipher is not null;

    /// <summary>Opens an archive file and validates its manifest.</summary>
    /// <param name="path">The archive path.</param>
    /// <returns>The reader.</returns>
    /// <exception cref="BackupIntegrityException">Thrown when the archive or manifest is invalid.</exception>
    public static BackupArchiveReader Open(string path)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.RandomAccess);
        try
        {
            return Open(stream);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    /// <summary>Opens an archive stream (seekable) and validates its manifest.</summary>
    /// <param name="stream">The archive stream (disposed with the reader).</param>
    /// <returns>The reader.</returns>
    /// <exception cref="BackupIntegrityException">Thrown when the archive or manifest is invalid.</exception>
    public static BackupArchiveReader Open(Stream stream)
    {
        ZipArchive zip;
        try
        {
            zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        }
        catch (InvalidDataException ex)
        {
            throw new BackupIntegrityException("The file is not a valid backup archive (zip).", ex);
        }

        try
        {
            var manifestEntry = zip.GetEntry(BackupArchiveFormat.ManifestEntry)
                ?? throw new BackupIntegrityException("The archive has no manifest. It is not a Servora backup or it is incomplete.");
            if (manifestEntry.Length > BackupArchiveFormat.MaxManifestBytes)
            {
                throw new BackupIntegrityException("The archive manifest is too large.");
            }

            var bytes = ReadAll(manifestEntry, BackupArchiveFormat.MaxManifestBytes);
            BackupManifest? manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<BackupManifest>(bytes, BackupJson.Options);
            }
            catch (JsonException ex)
            {
                throw new BackupIntegrityException("The archive manifest is not valid JSON.", ex);
            }

            var problems = BackupManifestValidator.Validate(manifest);
            if (problems.Count > 0)
            {
                throw new BackupIntegrityException("The archive manifest is invalid: " + string.Join(" ", problems.Take(5)));
            }

            return new BackupArchiveReader(zip, manifest!, bytes);
        }
        catch (InvalidDataException ex)
        {
            zip.Dispose();
            throw new BackupIntegrityException("The backup archive is corrupt.", ex);
        }
        catch
        {
            zip.Dispose();
            throw;
        }
    }

    /// <summary>Unlocks an encrypted archive: checks the password and the manifest signature. No-op for unencrypted archives.</summary>
    /// <param name="password">The password.</param>
    /// <exception cref="BackupPasswordException">Thrown when the password is missing or wrong.</exception>
    /// <exception cref="BackupIntegrityException">Thrown when the manifest signature does not match.</exception>
    public void Unlock(string? password)
    {
        if (!IsEncrypted || _cipher is not null)
        {
            return;
        }

        var cipher = BackupCipher.Open(password ?? string.Empty, Manifest.Encryption!);
        var signatureEntry = _zip.GetEntry(BackupArchiveFormat.SignatureEntry);
        if (signatureEntry is null || signatureEntry.Length > 1024 || !cipher.Verify(_manifestBytes, ReadAll(signatureEntry, 1024)))
        {
            cipher.Dispose();
            throw new BackupIntegrityException("The manifest signature of the encrypted archive does not match. The archive was modified.");
        }

        _cipher = cipher;
    }

    /// <summary>Verifies that the archive contains exactly the manifest entries with matching lengths and SHA-256 hashes.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The verification result.</returns>
    public async Task<BackupVerificationResult> VerifyAsync(CancellationToken cancellationToken = default)
    {
        var problems = new List<string>();
        long bytes = 0;
        var checkedEntries = 0;
        var expected = Manifest.Entries.ToDictionary(e => e.Path, StringComparer.Ordinal);

        foreach (var zipEntry in _zip.Entries)
        {
            if (zipEntry.FullName is BackupArchiveFormat.ManifestEntry || (zipEntry.FullName == BackupArchiveFormat.SignatureEntry && IsEncrypted))
            {
                continue;
            }

            if (!expected.ContainsKey(zipEntry.FullName))
            {
                problems.Add($"Unexpected entry '{zipEntry.FullName}' (not listed in the manifest).");
            }
        }

        foreach (var entry in Manifest.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var zipEntry = _zip.GetEntry(entry.Path);
            if (zipEntry is null)
            {
                problems.Add($"Entry '{entry.Path}' is missing.");
                continue;
            }

            try
            {
                using var hashing = new HashingReadStream(zipEntry.Open(), entry.Length);
                await hashing.CopyToAsync(Stream.Null, cancellationToken);
                bytes += hashing.BytesRead;
                checkedEntries++;
                if (hashing.BytesRead != entry.Length)
                {
                    problems.Add($"Entry '{entry.Path}' has the wrong length.");
                }
                else if (!string.Equals(hashing.GetHash(), entry.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    problems.Add($"Entry '{entry.Path}' does not match its SHA-256 hash.");
                }
            }
            catch (Exception ex) when (ex is BackupIntegrityException or InvalidDataException or IOException)
            {
                problems.Add($"Entry '{entry.Path}' cannot be read: {ex.Message}");
            }
        }

        return new BackupVerificationResult(problems.Count == 0, checkedEntries, bytes, IsEncrypted && _cipher is not null, problems);
    }

    /// <summary>Opens an entry for reading its plain content (decrypted and inflated for encrypted archives).</summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The plain content stream.</returns>
    /// <exception cref="BackupPasswordException">Thrown when the archive is encrypted but not unlocked.</exception>
    public Stream OpenEntry(BackupEntryManifest entry)
    {
        var zipEntry = _zip.GetEntry(entry.Path) ?? throw new BackupIntegrityException($"Entry '{entry.Path}' is missing.");
        var raw = new HashingReadStream(zipEntry.Open(), entry.Length);
        if (!IsEncrypted)
        {
            return raw;
        }

        if (_cipher is null)
        {
            raw.Dispose();
            throw new BackupPasswordException("The backup is encrypted. Unlock it with the password first.");
        }

        return new OwningStream(new DeflateStream(_cipher.CreateDecryptor(raw, entry.Index, entry.Path), CompressionMode.Decompress, leaveOpen: false), raw);
    }

    /// <summary>Finds an entry of the manifest.</summary>
    /// <param name="path">The entry path.</param>
    /// <returns>The entry.</returns>
    public BackupEntryManifest GetEntry(string path) =>
        Manifest.Entries.FirstOrDefault(e => e.Path == path) ?? throw new BackupIntegrityException($"Entry '{path}' is missing.");

    /// <inheritdoc />
    public void Dispose()
    {
        _cipher?.Dispose();
        _zip.Dispose();
    }

    private static byte[] ReadAll(ZipArchiveEntry entry, long limit)
    {
        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = stream.Read(chunk)) > 0)
        {
            if (buffer.Length + read > limit)
            {
                throw new BackupIntegrityException($"The archive entry '{entry.FullName}' is too large.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
