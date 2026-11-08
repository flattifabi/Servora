namespace Servora.Application.Portal;

/// <summary>
/// Validates links of the portal settings (support button, footer links): site-relative paths and absolute
/// <c>http</c> / <c>https</c> URLs only. Scriptable schemes (<c>javascript:</c>, <c>data:</c>, …), protocol-relative URLs,
/// back-slashes, white space, control characters and user info are rejected.
/// </summary>
public static class PortalUrlPolicy
{
    /// <summary>The maximum link length.</summary>
    public const int MaxLength = 500;

    /// <summary>Checks whether a link may be stored and rendered.</summary>
    /// <param name="url">The link.</param>
    /// <returns><see langword="true"/> when the link is a safe site-relative path or http(s) URL.</returns>
    public static bool IsAllowed(string? url)
    {
        var link = url?.Trim();
        if (string.IsNullOrEmpty(link) || link.Length > MaxLength)
        {
            return false;
        }

        if (link.Any(c => char.IsControl(c) || char.IsWhiteSpace(c) || c == '\\'))
        {
            return false;
        }

        if (link.StartsWith('/'))
        {
            return link.Length == 1 || link[1] != '/';
        }

        return Uri.TryCreate(link, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
               && !string.IsNullOrEmpty(uri.Host)
               && string.IsNullOrEmpty(uri.UserInfo);
    }
}
