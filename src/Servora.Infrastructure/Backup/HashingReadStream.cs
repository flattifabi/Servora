using System.Security.Cryptography;

namespace Servora.Infrastructure.Backup;

/// <summary>
/// Read-only pass-through stream that counts the bytes and computes their SHA-256. Reading more than the expected
/// length fails immediately, which protects against decompression bombs in uploaded archives.
/// </summary>
/// <param name="inner">The inner stream (disposed with this stream).</param>
/// <param name="maxLength">The maximum number of bytes that may be read.</param>
internal sealed class HashingReadStream(Stream inner, long maxLength) : Stream
{
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

    /// <summary>Gets the number of bytes read.</summary>
    public long BytesRead { get; private set; }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => BytesRead;
        set => throw new NotSupportedException();
    }

    /// <summary>Completes the hash.</summary>
    /// <returns>The lower-case hexadecimal SHA-256.</returns>
    public string GetHash() => Convert.ToHexStringLower(_hash.GetHashAndReset());

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        var read = inner.Read(buffer);
        Account(buffer[..read]);
        return read;
    }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = await inner.ReadAsync(buffer, cancellationToken);
        Account(buffer.Span[..read]);
        return read;
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

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
        if (disposing)
        {
            _hash.Dispose();
            inner.Dispose();
        }

        base.Dispose(disposing);
    }

    private void Account(ReadOnlySpan<byte> data)
    {
        BytesRead += data.Length;
        if (BytesRead > maxLength)
        {
            throw new BackupIntegrityException("An archive entry is larger than declared in the manifest.");
        }

        _hash.AppendData(data);
    }
}
