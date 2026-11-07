using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace Servora.Infrastructure.Identity;

/// <summary>
/// Converts the binary form of a Windows security identifier (as returned in the AD <c>objectSid</c> attribute) to its
/// string form (<c>S-1-5-21-…</c>). Platform independent, no Windows API required.
/// </summary>
public static class SidFormatter
{
    /// <summary>Formats a binary SID.</summary>
    /// <param name="sid">The binary SID.</param>
    /// <returns>The string form, or <see langword="null"/> when the input is not a valid SID.</returns>
    public static string? Format(ReadOnlySpan<byte> sid)
    {
        // Layout: revision (1), sub-authority count (1), identifier authority (6, big endian), sub-authorities (4 each, little endian).
        if (sid.Length < 8)
        {
            return null;
        }

        var revision = sid[0];
        var count = sid[1];
        if (revision != 1 || count > 15 || sid.Length != 8 + (count * 4))
        {
            return null;
        }

        ulong authority = 0;
        for (var i = 2; i < 8; i++)
        {
            authority = (authority << 8) | sid[i];
        }

        var builder = new StringBuilder("S-1-");
        builder.Append(authority.ToString(CultureInfo.InvariantCulture));
        for (var i = 0; i < count; i++)
        {
            var value = BinaryPrimitives.ReadUInt32LittleEndian(sid.Slice(8 + (i * 4), 4));
            builder.Append('-').Append(value.ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}
