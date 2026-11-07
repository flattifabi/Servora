using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Servora.Infrastructure.Backup;

/// <summary>Read-only stream that decrypts and authenticates content written by <see cref="AesGcmChunkWriteStream"/>.</summary>
internal sealed class AesGcmChunkReadStream : Stream
{
    /// <summary>The AES-GCM tag size in bytes.</summary>
    public const int TagSize = 16;

    /// <summary>The AES-GCM nonce size in bytes.</summary>
    public const int NonceSize = 12;

    private readonly Stream _input;
    private readonly AesGcm _aes;
    private readonly int _entryIndex;
    private readonly byte[] _pathBytes;
    private readonly int _maxChunkSize;
    private byte[] _plain = [];
    private int _position;
    private int _length;
    private long _chunkIndex;
    private bool _finalSeen;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="AesGcmChunkReadStream"/> class.</summary>
    /// <param name="input">The encrypted input (left open).</param>
    /// <param name="key">The 256-bit key.</param>
    /// <param name="entryIndex">The entry index.</param>
    /// <param name="entryPath">The entry path.</param>
    /// <param name="maxChunkSize">The maximum plaintext chunk size.</param>
    public AesGcmChunkReadStream(Stream input, byte[] key, int entryIndex, string entryPath, int maxChunkSize)
    {
        _input = input;
        _aes = new AesGcm(key, TagSize);
        _entryIndex = entryIndex;
        _pathBytes = Encoding.UTF8.GetBytes(entryPath);
        _maxChunkSize = maxChunkSize;
    }

    /// <inheritdoc />
    public override bool CanRead => !_disposed;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>Builds the nonce of a chunk.</summary>
    /// <param name="nonce">The destination (12 bytes).</param>
    /// <param name="entryIndex">The entry index.</param>
    /// <param name="chunkIndex">The chunk index.</param>
    public static void BuildNonce(Span<byte> nonce, int entryIndex, long chunkIndex)
    {
        BinaryPrimitives.WriteInt32BigEndian(nonce, entryIndex);
        BinaryPrimitives.WriteInt64BigEndian(nonce[4..], chunkIndex);
    }

    /// <summary>Builds the additional authenticated data of a chunk.</summary>
    /// <param name="pathBytes">The UTF-8 entry path.</param>
    /// <param name="chunkIndex">The chunk index.</param>
    /// <param name="final">Whether this is the final chunk.</param>
    /// <returns>The additional data.</returns>
    public static byte[] BuildAssociatedData(byte[] pathBytes, long chunkIndex, bool final)
    {
        var aad = new byte[pathBytes.Length + 10];
        pathBytes.CopyTo(aad, 0);
        aad[pathBytes.Length] = 0;
        BinaryPrimitives.WriteInt64BigEndian(aad.AsSpan(pathBytes.Length + 1), chunkIndex);
        aad[^1] = final ? (byte)1 : (byte)0;
        return aad;
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        while (_position == _length)
        {
            if (_finalSeen)
            {
                return 0;
            }

            ReadChunk();
        }

        var take = Math.Min(buffer.Length, _length - _position);
        _plain.AsSpan(_position, take).CopyTo(buffer);
        _position += take;
        return take;
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        Task.FromResult(Read(buffer.AsSpan(offset, count)));

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Read(buffer.Span));

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _aes.Dispose();
            CryptographicOperations.ZeroMemory(_plain);
        }

        _disposed = true;
        base.Dispose(disposing);
    }

    private void ReadChunk()
    {
        Span<byte> header = stackalloc byte[5];
        var read = _input.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
        if (read < header.Length)
        {
            throw new BackupIntegrityException("An encrypted archive entry is truncated.");
        }

        if (header[0] > 1)
        {
            throw new BackupIntegrityException("An encrypted archive entry is malformed.");
        }

        var final = header[0] == 1;
        var length = BinaryPrimitives.ReadInt32LittleEndian(header[1..]);
        if (length < 0 || length > _maxChunkSize)
        {
            throw new BackupIntegrityException("An encrypted archive entry has an invalid chunk length.");
        }

        var cipherText = new byte[length];
        var tag = new byte[TagSize];
        try
        {
            _input.ReadExactly(cipherText);
            _input.ReadExactly(tag);
        }
        catch (EndOfStreamException ex)
        {
            throw new BackupIntegrityException("An encrypted archive entry is truncated.", ex);
        }

        Span<byte> nonce = stackalloc byte[NonceSize];
        BuildNonce(nonce, _entryIndex, _chunkIndex);
        var aad = BuildAssociatedData(_pathBytes, _chunkIndex, final);
        if (_plain.Length < length)
        {
            _plain = new byte[Math.Max(length, _maxChunkSize)];
        }

        try
        {
            _aes.Decrypt(nonce, cipherText, tag, _plain.AsSpan(0, length), aad);
        }
        catch (AuthenticationTagMismatchException ex)
        {
            throw new BackupIntegrityException("An encrypted archive entry failed authentication (tampered or wrong key).", ex);
        }

        _position = 0;
        _length = length;
        _chunkIndex++;
        if (final)
        {
            _finalSeen = true;
            Span<byte> probe = stackalloc byte[1];
            if (_input.Read(probe) != 0)
            {
                throw new BackupIntegrityException("An encrypted archive entry has data after its final chunk.");
            }
        }
    }
}
