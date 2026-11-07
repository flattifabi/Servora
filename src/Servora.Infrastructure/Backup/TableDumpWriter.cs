using System.Text;

namespace Servora.Infrastructure.Backup;

/// <summary>
/// Writes a table dump: header <c>"SVTD"</c>, format byte, column count; then per row a <c>1</c> marker followed by the
/// typed values (<see cref="BackupValueCodec"/>); a <c>0</c> marker ends the dump.
/// </summary>
public sealed class TableDumpWriter : IDisposable
{
    /// <summary>The magic bytes of a table dump.</summary>
    internal static readonly byte[] Magic = "SVTD"u8.ToArray();

    /// <summary>The dump format version.</summary>
    internal const byte Version = 1;

    private readonly BinaryWriter _writer;
    private readonly int _columnCount;
    private bool _completed;

    /// <summary>Initializes a new instance of the <see cref="TableDumpWriter"/> class and writes the header.</summary>
    /// <param name="output">The output stream (left open).</param>
    /// <param name="columnCount">The number of columns per row.</param>
    public TableDumpWriter(Stream output, int columnCount)
    {
        _writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        _columnCount = columnCount;
        _writer.Write(Magic);
        _writer.Write(Version);
        _writer.Write(columnCount);
    }

    /// <summary>Gets the number of rows written.</summary>
    public long RowCount { get; private set; }

    /// <summary>Writes a row.</summary>
    /// <param name="values">The values in column order.</param>
    public void WriteRow(ReadOnlySpan<object?> values)
    {
        if (values.Length != _columnCount)
        {
            throw new ArgumentException("The number of values does not match the column count.", nameof(values));
        }

        _writer.Write((byte)1);
        foreach (var value in values)
        {
            BackupValueCodec.Write(_writer, value);
        }

        RowCount++;
    }

    /// <summary>Writes the end marker and flushes.</summary>
    public void Complete()
    {
        if (!_completed)
        {
            _writer.Write((byte)0);
            _writer.Flush();
            _completed = true;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Complete();
        _writer.Dispose();
    }
}
