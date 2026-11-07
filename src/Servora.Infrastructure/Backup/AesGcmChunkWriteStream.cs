using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Servora.Infrastructure.Backup;

/// <summary>
/// Write-only stream that encrypts its content with AES-256-GCM in chunks. Chunk layout:
/// <c>[flag:1][length:4 LE][ciphertext:length][tag:16]</c>; flag 1 marks the final chunk. The nonce is
/// <c>[entry index:4 BE][chunk index:8 BE]</c>; the additional data binds the entry path, chunk index and flag so
/// chunks cannot be reordered, moved between entries or truncated unnoticed.
/// </summary>
internal sealed class AesGcmChunkWriteStream : Stream
{
    private readonly Stream _output;
    private readonly AesGcm _aes;
    private readonly int _entryIndex;
    private readonly byte[] _pathBytes;
    private readonly byte[] _buffer;
    private int _count;
    private long _chunkIndex;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="AesGcmChunkWriteStream"/> class.</summary>
    /// <param name="output">The output stream (left open).</param>
    /// <param name="key">The 256-bit key.</param>
    /// <param name="entryIndex">The entry index.</param>
    /// <param name="entryPath">The entry path.</param>
    /// <param name="chunkSize">The plaintext chunk size.</param>
    public AesGcmChunkWriteStream(Stream output, byte[] key, int entryIndex, string entryPath, int chunkSize)
    {
        _output = output;
        _aes = new AesGcm(key, AesGcmChunkReadStream.TagSize);
        _entryIndex = entryIndex;
        _pathBytes = Encoding.UTF8.GetBytes(entryPath);
        _buffer = new byte[chunkSize];
    }

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => !_disposed;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        while (!buffer.IsEmpty)
        {
            if (_count == _buffer.Length)
            {
                EmitChunk(final: false);
            }

            var take = Math.Min(buffer.Length, _buffer.Length - _count);
            buffer[..take].CopyTo(_buffer.AsSpan(_count));
            _count += take;
            buffer = buffer[take..];
        }
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        Write(buffer.AsSpan(offset, count));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public override void Flush()
    {
        // Chunks are emitted when full and on dispose; flushing early would only produce smaller chunks.
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            EmitChunk(final: true);
            _output.Flush();
            _aes.Dispose();
            CryptographicOperations.ZeroMemory(_buffer);
        }

        _disposed = true;
        base.Dispose(disposing);
    }

    private void EmitChunk(bool final)
    {
        Span<byte> nonce = stackalloc byte[AesGcmChunkReadStream.NonceSize];
        AesGcmChunkReadStream.BuildNonce(nonce, _entryIndex, _chunkIndex);
        var aad = AesGcmChunkReadStream.BuildAssociatedData(_pathBytes, _chunkIndex, final);
        var cipherText = new byte[_count];
        var tag = new byte[AesGcmChunkReadStream.TagSize];
        _aes.Encrypt(nonce, _buffer.AsSpan(0, _count), cipherText, tag, aad);

        Span<byte> header = stackalloc byte[5];
        header[0] = final ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteInt32LittleEndian(header[1..], _count);
        _output.Write(header);
        _output.Write(cipherText);
        _output.Write(tag);

        _count = 0;
        _chunkIndex++;
    }
}
