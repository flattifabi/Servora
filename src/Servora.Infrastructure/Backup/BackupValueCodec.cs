namespace Servora.Infrastructure.Backup;

/// <summary>Binary, type-preserving encoding of single database values (see <see cref="BackupValueType"/>).</summary>
public static class BackupValueCodec
{
    /// <summary>The largest string or byte array accepted when reading (1 GiB).</summary>
    public const int MaxBlobLength = 1 << 30;

    /// <summary>Writes a value.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The value (<see langword="null"/> or <see cref="DBNull"/> for null).</param>
    /// <exception cref="NotSupportedException">Thrown for CLR types without an encoding.</exception>
    public static void Write(BinaryWriter writer, object? value)
    {
        switch (value)
        {
            case null or DBNull:
                writer.Write((byte)BackupValueType.Null);
                break;
            case string s:
                writer.Write((byte)BackupValueType.String);
                writer.Write(s);
                break;
            case bool b:
                writer.Write((byte)BackupValueType.Boolean);
                writer.Write(b);
                break;
            case byte b:
                writer.Write((byte)BackupValueType.Byte);
                writer.Write(b);
                break;
            case short s:
                writer.Write((byte)BackupValueType.Int16);
                writer.Write(s);
                break;
            case int i:
                writer.Write((byte)BackupValueType.Int32);
                writer.Write(i);
                break;
            case long l:
                writer.Write((byte)BackupValueType.Int64);
                writer.Write(l);
                break;
            case float f:
                writer.Write((byte)BackupValueType.Single);
                writer.Write(f);
                break;
            case double d:
                writer.Write((byte)BackupValueType.Double);
                writer.Write(d);
                break;
            case decimal m:
                writer.Write((byte)BackupValueType.Decimal);
                writer.Write(m);
                break;
            case Guid g:
                writer.Write((byte)BackupValueType.Guid);
                Span<byte> guidBytes = stackalloc byte[16];
                g.TryWriteBytes(guidBytes);
                writer.Write(guidBytes);
                break;
            case DateTime dt:
                writer.Write((byte)BackupValueType.DateTime);
                writer.Write(dt.Ticks);
                writer.Write((byte)dt.Kind);
                break;
            case DateTimeOffset dto:
                writer.Write((byte)BackupValueType.DateTimeOffset);
                writer.Write(dto.Ticks);
                writer.Write((short)dto.Offset.TotalMinutes);
                break;
            case TimeSpan ts:
                writer.Write((byte)BackupValueType.TimeSpan);
                writer.Write(ts.Ticks);
                break;
            case DateOnly d:
                writer.Write((byte)BackupValueType.DateOnly);
                writer.Write(d.DayNumber);
                break;
            case TimeOnly t:
                writer.Write((byte)BackupValueType.TimeOnly);
                writer.Write(t.Ticks);
                break;
            case byte[] bytes:
                writer.Write((byte)BackupValueType.Bytes);
                writer.Write7BitEncodedInt(bytes.Length);
                writer.Write(bytes);
                break;
            case char c:
                writer.Write((byte)BackupValueType.Char);
                writer.Write((ushort)c);
                break;
            case sbyte sb:
                writer.Write((byte)BackupValueType.SByte);
                writer.Write(sb);
                break;
            case ushort us:
                writer.Write((byte)BackupValueType.UInt16);
                writer.Write(us);
                break;
            case uint ui:
                writer.Write((byte)BackupValueType.UInt32);
                writer.Write(ui);
                break;
            case ulong ul:
                writer.Write((byte)BackupValueType.UInt64);
                writer.Write(ul);
                break;
            default:
                throw new NotSupportedException($"Values of type '{value.GetType().FullName}' cannot be backed up.");
        }
    }

    /// <summary>Reads a value.</summary>
    /// <param name="reader">The reader.</param>
    /// <returns>The value, or <see langword="null"/> for a database null.</returns>
    /// <exception cref="BackupIntegrityException">Thrown for unknown type tags or invalid lengths.</exception>
    public static object? Read(BinaryReader reader)
    {
        var type = (BackupValueType)reader.ReadByte();
        return type switch
        {
            BackupValueType.Null => null,
            BackupValueType.String => reader.ReadString(),
            BackupValueType.Boolean => reader.ReadBoolean(),
            BackupValueType.Byte => reader.ReadByte(),
            BackupValueType.Int16 => reader.ReadInt16(),
            BackupValueType.Int32 => reader.ReadInt32(),
            BackupValueType.Int64 => reader.ReadInt64(),
            BackupValueType.Single => reader.ReadSingle(),
            BackupValueType.Double => reader.ReadDouble(),
            BackupValueType.Decimal => reader.ReadDecimal(),
            BackupValueType.Guid => new Guid(reader.ReadBytes(16)),
            BackupValueType.DateTime => ReadDateTime(reader),
            BackupValueType.DateTimeOffset => new DateTimeOffset(reader.ReadInt64(), TimeSpan.FromMinutes(reader.ReadInt16())),
            BackupValueType.TimeSpan => new TimeSpan(reader.ReadInt64()),
            BackupValueType.DateOnly => DateOnly.FromDayNumber(reader.ReadInt32()),
            BackupValueType.TimeOnly => new TimeOnly(reader.ReadInt64()),
            BackupValueType.Bytes => ReadBytes(reader),
            BackupValueType.Char => (char)reader.ReadUInt16(),
            BackupValueType.SByte => reader.ReadSByte(),
            BackupValueType.UInt16 => reader.ReadUInt16(),
            BackupValueType.UInt32 => reader.ReadUInt32(),
            BackupValueType.UInt64 => reader.ReadUInt64(),
            _ => throw new BackupIntegrityException($"Unknown value type {(byte)type} in a table dump."),
        };
    }

    private static DateTime ReadDateTime(BinaryReader reader)
    {
        var ticks = reader.ReadInt64();
        var kind = (DateTimeKind)reader.ReadByte();
        if (!Enum.IsDefined(kind))
        {
            throw new BackupIntegrityException("Invalid date/time kind in a table dump.");
        }

        return new DateTime(ticks, kind);
    }

    private static byte[] ReadBytes(BinaryReader reader)
    {
        var length = reader.Read7BitEncodedInt();
        if (length is < 0 or > MaxBlobLength)
        {
            throw new BackupIntegrityException("Invalid binary length in a table dump.");
        }

        var bytes = reader.ReadBytes(length);
        if (bytes.Length != length)
        {
            throw new BackupIntegrityException("A table dump is truncated.");
        }

        return bytes;
    }
}
