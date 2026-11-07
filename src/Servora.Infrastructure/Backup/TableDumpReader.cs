using System.Text;

namespace Servora.Infrastructure.Backup;

/// <summary>Reads a table dump written by <see cref="TableDumpWriter"/> row by row.</summary>
public sealed class TableDumpReader : IDisposable
{
    private readonly BinaryReader _reader;
    private bool _ended;

    /// <summary>Initializes a new instance of the <see cref="TableDumpReader"/> class and validates the header.</summary>
    /// <param name="input">The plain dump stream (disposed with the reader).</param>
    /// <param name="expectedColumns">The column count declared in the manifest.</param>
    /// <exception cref="BackupIntegrityException">Thrown when the header is invalid.</exception>
    public TableDumpReader(Stream input, int expectedColumns)
    {
        _reader = new BinaryReader(input, Encoding.UTF8, leaveOpen: false);
        try
        {
            var magic = _reader.ReadBytes(TableDumpWriter.Magic.Length);
            if (!magic.AsSpan().SequenceEqual(TableDumpWriter.Magic) || _reader.ReadByte() != TableDumpWriter.Version)
            {
                throw new BackupIntegrityException("A table dump has an invalid header.");
            }

            ColumnCount = _reader.ReadInt32();
        }
        catch (EndOfStreamException ex)
        {
            throw new BackupIntegrityException("A table dump is truncated.", ex);
        }

        if (ColumnCount != expectedColumns)
        {
            throw new BackupIntegrityException("A table dump does not match the column list of the manifest.");
        }
    }

    /// <summary>Gets the number of columns per row.</summary>
    public int ColumnCount { get; }

    /// <summary>Reads the next row.</summary>
    /// <param name="values">The destination (length = <see cref="ColumnCount"/>).</param>
    /// <returns><see langword="false"/> at the end of the dump.</returns>
    /// <exception cref="BackupIntegrityException">Thrown when the dump is malformed or truncated.</exception>
    public bool ReadRow(object?[] values)
    {
        if (_ended)
        {
            return false;
        }

        try
        {
            var marker = _reader.ReadByte();
            if (marker == 0)
            {
                _ended = true;
                return false;
            }

            if (marker != 1)
            {
                throw new BackupIntegrityException("A table dump is malformed.");
            }

            for (var i = 0; i < ColumnCount; i++)
            {
                values[i] = BackupValueCodec.Read(_reader);
            }

            return true;
        }
        catch (EndOfStreamException ex)
        {
            throw new BackupIntegrityException("A table dump is truncated.", ex);
        }
    }

    /// <inheritdoc />
    public void Dispose() => _reader.Dispose();
}
