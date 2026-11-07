using System.Text;
using System.Text.RegularExpressions;

namespace Servora.Application.Identity.Ldap;

/// <summary>
/// Safe construction and validation of LDAP search filters (RFC 4515). User supplied values are always escaped, so a
/// login name such as <c>*)(uid=*</c> can never change the structure of a filter.
/// </summary>
public static partial class LdapFilter
{
    /// <summary>The maximum accepted length of an administrator supplied filter.</summary>
    public const int MaxFilterLength = 2048;

    /// <summary>
    /// Escapes an assertion value according to RFC 4515 section 3: <c>\</c>, <c>*</c>, <c>(</c>, <c>)</c> and NUL are
    /// replaced by a backslash followed by two hex digits. Other control characters are escaped as well.
    /// </summary>
    /// <param name="value">The raw value.</param>
    /// <returns>The escaped value.</returns>
    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length + 8);
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\':
                    builder.Append(@"\5c");
                    break;
                case '*':
                    builder.Append(@"\2a");
                    break;
                case '(':
                    builder.Append(@"\28");
                    break;
                case ')':
                    builder.Append(@"\29");
                    break;
                case '\0':
                    builder.Append(@"\00");
                    break;
                default:
                    if (c < 0x20 || c == 0x7f)
                    {
                        builder.Append('\\').Append(((int)c).ToString("x2"));
                    }
                    else
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }

        return builder.ToString();
    }

    /// <summary>Escapes a binary value (for example an <c>objectSid</c> or <c>objectGUID</c>) as <c>\xx</c> sequences.</summary>
    /// <param name="value">The bytes.</param>
    /// <returns>The escaped value.</returns>
    public static string EscapeBinary(ReadOnlySpan<byte> value)
    {
        var builder = new StringBuilder(value.Length * 3);
        foreach (var b in value)
        {
            builder.Append('\\').Append(b.ToString("x2"));
        }

        return builder.ToString();
    }

    /// <summary>Builds an equality filter <c>(attribute=value)</c> with an escaped value.</summary>
    /// <param name="attribute">The attribute description (validated).</param>
    /// <param name="value">The raw value.</param>
    /// <returns>The filter.</returns>
    /// <exception cref="ArgumentException">The attribute name is invalid.</exception>
    public static string Equal(string attribute, string value)
    {
        if (!IsValidAttributeName(attribute))
        {
            throw new ArgumentException($"'{attribute}' is not a valid LDAP attribute name.", nameof(attribute));
        }

        return $"({attribute}={Escape(value)})";
    }

    /// <summary>Combines filters with a logical AND. A single filter is returned unchanged.</summary>
    /// <param name="filters">The filters.</param>
    /// <returns>The combined filter.</returns>
    public static string And(params string[] filters) => Combine('&', filters);

    /// <summary>Combines filters with a logical OR. A single filter is returned unchanged.</summary>
    /// <param name="filters">The filters.</param>
    /// <returns>The combined filter.</returns>
    public static string Or(params string[] filters) => Combine('|', filters);

    /// <summary>Checks whether a string is a valid LDAP attribute description (name or numeric OID, no options).</summary>
    /// <param name="attribute">The attribute.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    public static bool IsValidAttributeName(string? attribute) =>
        !string.IsNullOrEmpty(attribute) && attribute.Length <= 64 && (AttributeNameRegex().IsMatch(attribute) || OidRegex().IsMatch(attribute));

    /// <summary>
    /// Validates an administrator supplied filter: it must be parenthesised, balanced, at most
    /// <see cref="MaxFilterLength"/> characters long and must not contain NUL or line breaks.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <returns><see langword="true"/> when the filter is structurally valid.</returns>
    public static bool IsValidFilter(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter) || filter.Length > MaxFilterLength)
        {
            return false;
        }

        filter = filter.Trim();
        if (filter[0] != '(' || filter[^1] != ')')
        {
            return false;
        }

        var depth = 0;
        for (var i = 0; i < filter.Length; i++)
        {
            var c = filter[i];
            switch (c)
            {
                case '\0' or '\r' or '\n':
                    return false;
                case '\\':
                    // An escape must be followed by two hex digits.
                    if (i + 2 >= filter.Length || !Uri.IsHexDigit(filter[i + 1]) || !Uri.IsHexDigit(filter[i + 2]))
                    {
                        return false;
                    }

                    i += 2;
                    break;
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    if (depth < 0)
                    {
                        return false;
                    }

                    // The outer group must close exactly at the end.
                    if (depth == 0 && i != filter.Length - 1)
                    {
                        return false;
                    }

                    break;
            }
        }

        return depth == 0;
    }

    /// <summary>
    /// Builds the filter used to find a user by login name:
    /// <c>(&amp;(userFilter)(userNameAttribute=account))</c>, or by UPN when a UPN was typed and a UPN attribute is configured.
    /// </summary>
    /// <param name="settings">The directory settings (attribute names and user filter must be valid).</param>
    /// <param name="login">The parsed login name.</param>
    /// <returns>The filter.</returns>
    public static string BuildUserLookup(DirectorySettings settings, DirectoryLoginName login)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(login);
        var identity = login.UserPrincipalName is { } upn && !string.IsNullOrWhiteSpace(settings.UpnAttribute)
            ? Equal(settings.UpnAttribute!, upn)
            : Equal(settings.UserNameAttribute, login.Account);
        return And(settings.UserFilter.Trim(), identity);
    }

    private static string Combine(char op, string[] filters)
    {
        var parts = filters.Where(f => !string.IsNullOrWhiteSpace(f)).ToArray();
        return parts.Length switch
        {
            0 => throw new ArgumentException("At least one filter is required.", nameof(filters)),
            1 => parts[0],
            _ => $"({op}{string.Concat(parts)})",
        };
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex AttributeNameRegex();

    [GeneratedRegex(@"^[0-9]+(\.[0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex OidRegex();
}
