using System.Globalization;
using System.Text;

namespace Servora.Application.Identity.Ldap;

/// <summary>Minimal, allocation-light helpers for comparing LDAP distinguished names (RFC 4514).</summary>
public static class DistinguishedName
{
    /// <summary>
    /// Normalises a DN for comparison: splits it into RDNs at unescaped commas, trims whitespace around RDNs and the
    /// <c>=</c> sign, unescapes values and lower-cases everything (invariant).
    /// </summary>
    /// <param name="dn">The distinguished name.</param>
    /// <returns>The normalised DN, or an empty string for empty input.</returns>
    public static string Normalize(string? dn)
    {
        if (string.IsNullOrWhiteSpace(dn))
        {
            return string.Empty;
        }

        var parts = SplitRdns(dn)
            .Select(rdn =>
            {
                var eq = IndexOfUnescaped(rdn, '=');
                if (eq < 0)
                {
                    return Unescape(rdn.Trim()).ToLowerInvariant();
                }

                return string.Concat(rdn[..eq].Trim().ToLowerInvariant(), "=", Unescape(rdn[(eq + 1)..].Trim()).ToLowerInvariant());
            });
        return string.Join(',', parts);
    }

    /// <summary>Gets the (unescaped) value of the first RDN, for example <c>Servora Admins</c> for <c>CN=Servora Admins,OU=Groups,…</c>.</summary>
    /// <param name="dn">The distinguished name.</param>
    /// <returns>The value or <see langword="null"/> for malformed input.</returns>
    public static string? GetLeafValue(string? dn)
    {
        if (string.IsNullOrWhiteSpace(dn))
        {
            return null;
        }

        var first = SplitRdns(dn).FirstOrDefault();
        if (first is null)
        {
            return null;
        }

        var eq = IndexOfUnescaped(first, '=');
        return eq < 0 ? null : Unescape(first[(eq + 1)..].Trim());
    }

    /// <summary>Checks whether two DNs are equal after normalisation.</summary>
    /// <param name="left">The first DN.</param>
    /// <param name="right">The second DN.</param>
    /// <returns><see langword="true"/> when equal.</returns>
    public static bool AreEqual(string? left, string? right)
    {
        var a = Normalize(left);
        return a.Length > 0 && string.Equals(a, Normalize(right), StringComparison.Ordinal);
    }

    private static List<string> SplitRdns(string dn)
    {
        var result = new List<string>();
        var start = 0;
        for (var i = 0; i < dn.Length; i++)
        {
            if (dn[i] == '\\')
            {
                i++;
                continue;
            }

            if (dn[i] is ',' or ';')
            {
                result.Add(dn[start..i]);
                start = i + 1;
            }
        }

        result.Add(dn[start..]);
        return result.Where(r => r.Trim().Length > 0).ToList();
    }

    private static int IndexOfUnescaped(string value, char target)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\')
            {
                i++;
                continue;
            }

            if (value[i] == target)
            {
                return i;
            }
        }

        return -1;
    }

    private static string Unescape(string value)
    {
        if (!value.Contains('\\'))
        {
            return value;
        }

        var bytes = new List<byte>();
        var builder = new StringBuilder();
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 2 < value.Length && Uri.IsHexDigit(value[i + 1]) && Uri.IsHexDigit(value[i + 2]))
            {
                bytes.Add(byte.Parse(value.AsSpan(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                i += 2;
                continue;
            }

            FlushBytes(bytes, builder);
            if (value[i] == '\\' && i + 1 < value.Length)
            {
                builder.Append(value[i + 1]);
                i++;
            }
            else
            {
                builder.Append(value[i]);
            }
        }

        FlushBytes(bytes, builder);
        return builder.ToString();
    }

    private static void FlushBytes(List<byte> bytes, StringBuilder builder)
    {
        if (bytes.Count > 0)
        {
            builder.Append(Encoding.UTF8.GetString(bytes.ToArray()));
            bytes.Clear();
        }
    }
}
