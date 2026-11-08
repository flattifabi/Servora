using System.Security.Cryptography;
using System.Text;

namespace Servora.Web.Security;

/// <summary>
/// Instance-specific cookie names. Browsers do not isolate cookies by port, so several Servora instances on the same
/// host (for example <c>localhost:5080</c> and <c>localhost:5090</c>) would overwrite each other's authentication and
/// antiforgery cookies (each instance has its own Data Protection keys, so a foreign cookie cannot be decrypted and the
/// user is signed out or the form post fails with HTTP 400). The names therefore carry a short, stable suffix derived
/// from the instance's data directory.
/// </summary>
public sealed class InstanceCookieNames
{
    /// <summary>Prefix of the authentication cookie name.</summary>
    public const string AuthenticationPrefix = "servora.auth";

    /// <summary>Prefix of the antiforgery cookie name.</summary>
    public const string AntiforgeryPrefix = "servora.af";

    /// <summary>Prefix of the one-shot cookie that hands the saved user theme to the browser after sign-in.</summary>
    public const string ThemePrefix = "servora.theme";

    /// <summary>Initializes a new instance of the <see cref="InstanceCookieNames"/> class.</summary>
    /// <param name="dataDirectory">The data directory of the instance.</param>
    public InstanceCookieNames(string dataDirectory)
    {
        Suffix = ComputeSuffix(dataDirectory);
    }

    /// <summary>Gets the instance suffix (8 lower-case hexadecimal characters).</summary>
    public string Suffix { get; }

    /// <summary>Gets the authentication cookie name.</summary>
    public string Authentication => $"{AuthenticationPrefix}.{Suffix}";

    /// <summary>Gets the antiforgery cookie name.</summary>
    public string Antiforgery => $"{AntiforgeryPrefix}.{Suffix}";

    /// <summary>Gets the name of the one-shot theme cookie.</summary>
    public string Theme => $"{ThemePrefix}.{Suffix}";

    /// <summary>Computes the stable instance suffix of a data directory.</summary>
    /// <param name="dataDirectory">The data directory.</param>
    /// <returns>The first 8 hexadecimal characters of the SHA-256 of the normalized full path.</returns>
    public static string ComputeSuffix(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataDirectory));
        if (OperatingSystem.IsWindows())
        {
            // Windows paths are case-insensitive: C:\Data and c:\data are the same instance.
            normalized = normalized.ToUpperInvariant();
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexStringLower(hash.AsSpan(0, 4));
    }
}
