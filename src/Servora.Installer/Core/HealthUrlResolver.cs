using System.Globalization;
using System.Text.RegularExpressions;
using Servora.Installer.Model;

namespace Servora.Installer.Core;

/// <summary>
/// Derives the local health URL from the configured listen URLs. Wildcard hosts (<c>0.0.0.0</c>, <c>*</c>, <c>+</c>,
/// <c>[::]</c>) are probed via <c>localhost</c>; plain HTTP endpoints are preferred because the TLS certificate of an
/// HTTPS endpoint is usually issued for the public host name, not for <c>localhost</c>.
/// </summary>
internal static partial class HealthUrlResolver
{
    /// <summary>Resolves the health URL.</summary>
    /// <param name="configuredUrls">Configured listen URLs (instance settings first, then shipped defaults).</param>
    /// <returns>The health URL.</returns>
    public static Uri Resolve(IReadOnlyList<string> configuredUrls)
    {
        var parsed = configuredUrls
            .Select(Parse)
            .Where(u => u is not null)
            .Select(u => u!.Value)
            .ToList();

        var chosen = parsed.FirstOrDefault(u => u.Scheme == "http");
        if (chosen == default)
        {
            chosen = parsed.FirstOrDefault();
        }

        if (chosen == default)
        {
            chosen = ("http", "localhost", ProductInfo.DefaultPort);
        }

        return new Uri(string.Create(CultureInfo.InvariantCulture, $"{chosen.Scheme}://{chosen.Host}:{chosen.Port}{ProductInfo.HealthPath}"));
    }

    /// <summary>Resolves the browser base URL (same rules as <see cref="Resolve"/>, without the health path).</summary>
    /// <param name="configuredUrls">Configured listen URLs.</param>
    /// <returns>The base URL ending with a slash.</returns>
    public static Uri ResolveBase(IReadOnlyList<string> configuredUrls) => new(Resolve(configuredUrls), "/");

    /// <summary>Extracts the port of the first configured URL.</summary>
    /// <param name="configuredUrls">Configured listen URLs.</param>
    /// <returns>The port, or <see langword="null"/>.</returns>
    public static int? FirstPort(IReadOnlyList<string> configuredUrls) =>
        configuredUrls.Select(Parse).FirstOrDefault(u => u is not null)?.Port;

    private static (string Scheme, string Host, int Port)? Parse(string url)
    {
        var match = UrlPattern().Match(url.Trim());
        if (!match.Success)
        {
            return null;
        }

        var scheme = match.Groups["scheme"].Value.ToLowerInvariant();
        var host = match.Groups["host"].Value;
        if (host is "0.0.0.0" or "*" or "+" or "[::]" or "::" || host.Equals("[::0]", StringComparison.Ordinal))
        {
            host = "localhost";
        }

        var port = match.Groups["port"].Success
            ? int.Parse(match.Groups["port"].Value, CultureInfo.InvariantCulture)
            : scheme == "https" ? 443 : 80;
        return port is < 1 or > 65535 ? null : (scheme, host, port);
    }

    [GeneratedRegex(@"^(?<scheme>https?)://(?<host>\[[^\]]+\]|[^:/;\s]+)(?::(?<port>\d{1,5}))?(?:/.*)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlPattern();
}
