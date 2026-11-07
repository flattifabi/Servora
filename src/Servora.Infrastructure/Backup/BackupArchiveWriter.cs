using System.IO.Compression;
using System.Text.Json;

namespace Servora.Infrastructure.Backup;

/// <summary>
/// Writes a <c>.svbak</c> archive entry by entry (streamed, never buffering a whole entry) and finally the manifest.
/// With a <see cref="BackupCipher"/> every entry is deflated, then encrypted, and stored without zip compression.
/// </summary>
public sealed class BackupArchiveWriter : IDisposable
{
    private readonly ZipArchive _zip;
    private readonly BackupCipher? _cipher;
    private readonly List<BackupEntryManifest> _entries = [];
    private readonly HashSet<string> _paths = new(StringComparer.OrdinalIgnoreCase);
    private bool _completed;

    /// <summary>Initializes a new instance of the <see cref="BackupArchiveWriter"/> class.</summary>
    /// <param name="output">The output stream (disposed with the writer).</param>
    /// <param name="cipher">The cipher for encrypted archives, or <see langword="null"/>.</param>
    public BackupArchiveWriter(Stream output, BackupCipher? cipher)
    {
        _zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: false);
        _cipher = cipher;
    }

    /// <summary>Gets the entries written so far.</summary>
    public IReadOnlyList<BackupEntryManifest> Entries => _entries;

    /// <summary>Writes an entry.</summary>
    /// <param name="path">The entry path (forward slashes).</param>
    /// <param name="kind">The entry kind.</param>
    /// <param name="write">Writes the plain content to the supplied stream.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The entry manifest.</returns>
    public async Task<BackupEntryManifest> WriteEntryAsync(string path, string kind, Func<Stream, CancellationToken, Task> write, CancellationToken cancellationToken)
    {
        if (_completed)
        {
            throw new InvalidOperationException("The archive is already completed.");
        }

        if (!BackupManifestValidator.IsSafeEntryPath(path) || !_paths.Add(path))
        {
            throw new BackupException($"The archive entry path '{path}' is invalid or duplicated.");
        }

        var index = _entries.Count;
        var entry = _zip.CreateEntry(path, _cipher is null ? CompressionLevel.Optimal : CompressionLevel.NoCompression);
        string hash;
        long length;
        await using (var zipStream = entry.Open())
        {
            using var hashing = new HashingWriteStream(zipStream);
            if (_cipher is null)
            {
                await write(hashing, cancellationToken);
            }
            else
            {
                await using (var encryptor = _cipher.CreateEncryptor(hashing, index, path))
                await using (var deflate = new DeflateStream(encryptor, CompressionLevel.Optimal, leaveOpen: true))
                {
                    await write(deflate, cancellationToken);
                }
            }

            await hashing.FlushAsync(cancellationToken);
            hash = hashing.GetHash();
            length = hashing.BytesWritten;
        }

        var manifestEntry = new BackupEntryManifest { Index = index, Path = path, Kind = kind, Length = length, Sha256 = hash };
        _entries.Add(manifestEntry);
        return manifestEntry;
    }

    /// <summary>Writes a file from disk as an entry.</summary>
    /// <param name="path">The entry path.</param>
    /// <param name="kind">The entry kind.</param>
    /// <param name="sourceFile">The file to copy.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The entry manifest.</returns>
    public Task<BackupEntryManifest> WriteFileAsync(string path, string kind, string sourceFile, CancellationToken cancellationToken) =>
        WriteEntryAsync(path, kind, async (stream, ct) =>
        {
            await using var source = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 81920, useAsync: true);
            await source.CopyToAsync(stream, ct);
        }, cancellationToken);

    /// <summary>Writes the manifest (and its signature for encrypted archives) and closes the archive.</summary>
    /// <param name="manifest">The manifest; its <see cref="BackupManifest.Entries"/> and encryption parameters are filled in.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the archive is closed.</returns>
    public async Task CompleteAsync(BackupManifest manifest, CancellationToken cancellationToken)
    {
        manifest.Entries = [.. _entries];
        manifest.Encryption = _cipher?.Parameters;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(manifest, BackupJson.Options);

        var manifestEntry = _zip.CreateEntry(BackupArchiveFormat.ManifestEntry, CompressionLevel.Optimal);
        await using (var stream = manifestEntry.Open())
        {
            await stream.WriteAsync(bytes, cancellationToken);
        }

        if (_cipher is not null)
        {
            var signatureEntry = _zip.CreateEntry(BackupArchiveFormat.SignatureEntry, CompressionLevel.NoCompression);
            await using var stream = signatureEntry.Open();
            await stream.WriteAsync(_cipher.Sign(bytes), cancellationToken);
        }

        _completed = true;
        _zip.Dispose();
    }

    /// <inheritdoc />
    public void Dispose() => _zip.Dispose();
}
