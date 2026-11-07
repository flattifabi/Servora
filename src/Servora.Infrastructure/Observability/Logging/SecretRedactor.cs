using System.Text.RegularExpressions;

namespace Servora.Infrastructure.Observability.Logging;

/// <summary>
/// Removes secrets from log text before it is buffered or written to disk: password / token / secret assignments
/// (<c>key=value</c>, <c>key: value</c>, JSON <c>"key":"value"</c>), connection string credentials, HTTP
/// <c>Authorization</c> values (Bearer / Basic), credentials embedded in URLs and JSON Web Tokens.
/// </summary>
public static partial class SecretRedactor
{
    /// <summary>The replacement text for redacted values.</summary>
    public const string Mask = "***";

    private const string SensitiveKeys =
        "password|passwd|pwd|pass|secret|client[_-]?secret|api[_-]?key|apikey|x-api-key|access[_-]?token|refresh[_-]?token|id[_-]?token|token|authorization|auth|credentials?|private[_-]?key|security[_-]?stamp|password[_-]?hash|connection[_-]?string|sas|sig|signature";

    private static readonly string[] s_sensitiveFragments =
    [
        "password", "passwd", "pwd", "secret", "token", "apikey", "api_key", "api-key", "authorization", "credential",
        "privatekey", "private_key", "securitystamp", "passwordhash", "connectionstring", "connection_string",
    ];

    /// <summary>Checks whether a property / parameter name denotes a secret (its value must never be logged).</summary>
    /// <param name="key">The property name.</param>
    /// <returns><see langword="true"/> when the value must be masked.</returns>
    public static bool IsSensitiveKey(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        var normalized = key.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        return normalized is "pass" or "auth" or "sig" || s_sensitiveFragments.Any(f => normalized.Contains(f, StringComparison.Ordinal));
    }

    /// <summary>Redacts secrets in a text.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The redacted text (the input instance when nothing matched).</returns>
    public static string? Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var result = JsonPair().Replace(text, m => $"{m.Groups["prefix"].Value}{Mask}\"");
        result = UrlCredentials().Replace(result, m => $"{m.Groups["scheme"].Value}{m.Groups["user"].Value}:{Mask}@");
        result = AuthorizationScheme().Replace(result, m => $"{m.Groups["scheme"].Value} {Mask}");
        result = Jwt().Replace(result, Mask);
        result = KeyValuePair().Replace(result, m => $"{m.Groups["key"].Value}{m.Groups["sep"].Value}{Mask}");
        return result;
    }

    /// <summary>JSON style <c>"password": "value"</c>.</summary>
    [GeneratedRegex("(?<prefix>\"[A-Za-z0-9_-]*?(?:" + SensitiveKeys + ")\"\\s*:\\s*\")(?:[^\"\\\\]|\\\\.)*\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex JsonPair();

    /// <summary>
    /// <c>key=value</c> / <c>key: value</c> pairs (connection strings, query strings, form data, plain text). The value ends
    /// at <c>;</c>, <c>&amp;</c>, <c>,</c>, whitespace or a quote.
    /// </summary>
    [GeneratedRegex("(?<key>\\b[A-Za-z0-9_-]*?(?:" + SensitiveKeys + "))(?<sep>\\s*[=:]\\s*)(?!\\*\\*\\*|(?:Bearer|Basic|Negotiate|NTLM|ApiKey|Token)\\s)(?:\"[^\"]*\"|'[^']*'|[^;&,\\s\"']+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex KeyValuePair();

    /// <summary><c>scheme://user:password@host</c>.</summary>
    [GeneratedRegex("(?<scheme>[a-z][a-z0-9+.-]*://)(?<user>[^:/@\\s]+):(?!\\*\\*\\*@)[^@/\\s]+@", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex UrlCredentials();

    /// <summary><c>Bearer xyz</c> / <c>Basic xyz</c> authorization values.</summary>
    [GeneratedRegex("(?<scheme>\\b(?:Bearer|Basic|Negotiate|NTLM|ApiKey|Token))\\s+(?!\\*\\*\\*)[A-Za-z0-9\\-._~+/]+=*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex AuthorizationScheme();

    /// <summary>JSON Web Tokens (three base64url segments starting with <c>eyJ</c>).</summary>
    [GeneratedRegex("eyJ[A-Za-z0-9_-]{5,}\\.[A-Za-z0-9_-]{5,}\\.[A-Za-z0-9_-]{5,}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex Jwt();
}
