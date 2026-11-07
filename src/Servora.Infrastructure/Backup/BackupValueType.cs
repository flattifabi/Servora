namespace Servora.Infrastructure.Backup;

/// <summary>Type tag of a value in a table dump. Values keep the CLR type returned by the provider's data reader.</summary>
public enum BackupValueType : byte
{
    /// <summary>A database null.</summary>
    Null = 0,

    /// <summary>A <see cref="string"/>.</summary>
    String = 1,

    /// <summary>A <see cref="bool"/>.</summary>
    Boolean = 2,

    /// <summary>A <see cref="byte"/>.</summary>
    Byte = 3,

    /// <summary>A <see cref="short"/>.</summary>
    Int16 = 4,

    /// <summary>An <see cref="int"/>.</summary>
    Int32 = 5,

    /// <summary>A <see cref="long"/>.</summary>
    Int64 = 6,

    /// <summary>A <see cref="float"/>.</summary>
    Single = 7,

    /// <summary>A <see cref="double"/>.</summary>
    Double = 8,

    /// <summary>A <see cref="decimal"/>.</summary>
    Decimal = 9,

    /// <summary>A <see cref="System.Guid"/>.</summary>
    Guid = 10,

    /// <summary>A <see cref="System.DateTime"/> including its <see cref="DateTimeKind"/>.</summary>
    DateTime = 11,

    /// <summary>A <see cref="System.DateTimeOffset"/>.</summary>
    DateTimeOffset = 12,

    /// <summary>A <see cref="System.TimeSpan"/>.</summary>
    TimeSpan = 13,

    /// <summary>A <see cref="System.DateOnly"/>.</summary>
    DateOnly = 14,

    /// <summary>A <see cref="System.TimeOnly"/>.</summary>
    TimeOnly = 15,

    /// <summary>A byte array.</summary>
    Bytes = 16,

    /// <summary>A <see cref="char"/>.</summary>
    Char = 17,

    /// <summary>An <see cref="sbyte"/>.</summary>
    SByte = 18,

    /// <summary>A <see cref="ushort"/>.</summary>
    UInt16 = 19,

    /// <summary>A <see cref="uint"/>.</summary>
    UInt32 = 20,

    /// <summary>A <see cref="ulong"/>.</summary>
    UInt64 = 21,
}
