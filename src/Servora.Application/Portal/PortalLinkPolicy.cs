namespace Servora.Application.Portal;

/// <summary>
/// Validates links entered by portal administrators (announcements). Only site-relative paths and absolute HTTPS URLs
/// are allowed; scriptable schemes, protocol-relative URLs, back-slashes and control characters are rejected.
/// </summary>
public static class PortalLinkPolicy
{
    /// <summary>Checks whether a link may be rendered on the portal.</summary>
    /// <param name="link">The link.</param>
    /// <returns><see langword="true"/> when the link is a safe site-relative path or HTTPS URL.</returns>
    public static bool IsAllowed(string? link)
    {
        if (string.IsNullOrWhiteSpace(link))
        {
            return false;
        }

        if (link.Any(c => char.IsControl(c) || char.IsWhiteSpace(c) || c == '\\'))
        {
            return false;
        }

        if (link.StartsWith('/'))
        {
            // "//host" would be protocol-relative and leave the site.
            return link.Length == 1 || link[1] != '/';
        }

        return Uri.TryCreate(link, UriKind.Absolute, out var uri)
               && uri.Scheme == Uri.UriSchemeHttps
               && !string.IsNullOrEmpty(uri.Host)
               && string.IsNullOrEmpty(uri.UserInfo);
    }
}
