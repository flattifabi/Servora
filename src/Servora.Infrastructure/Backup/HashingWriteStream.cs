using System.Security.Cryptography;

namespace Servora.Infrastructure.Backup;

/// <summary>Write-only pass-through stream that counts the bytes and computes their SHA-256.</summary>
/// <param name="inner">The inner stream (left open).</param>
internal sealed class HashingWriteStream(Stream inner) : Stream
{
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private string? _result;

    /// <summary>Gets the number of bytes written.</summary>
    public long BytesWritten { get; private set; }

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => true;

    /// <inheritdoc />
    public override long Length => BytesWritten;

    /// <inheritdoc />
    public override long Position
    {
        get => BytesWritten;
        set => throw new NotSupportedException();
    }

    /// <summary>Completes the hash.</summary>
    /// <returns>The lower-case hexadecimal SHA-256.</returns>
    public string GetHash() => _result ??= Convert.ToHexStringLower(_hash.GetHashAndReset());

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        _hash.AppendData(buffer);
        BytesWritten += buffer.Length;
        inner.Write(buffer);
    }

    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        _hash.AppendData(buffer.Span);
        BytesWritten += buffer.Length;
        await inner.WriteAsync(buffer, cancellationToken);
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override void Flush() => inner.Flush();

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hash.Dispose();
        }

        base.Dispose(disposing);
    }
}
