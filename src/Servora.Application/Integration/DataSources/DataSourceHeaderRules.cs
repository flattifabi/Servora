using System.Text.Json;
using System.Text.RegularExpressions;

namespace Servora.Application.Integration.DataSources;

/// <summary>Validation rules for configurable default request headers (no credentials, no hop-by-hop headers).</summary>
public static partial class DataSourceHeaderRules
{
    /// <summary>The maximum number of default headers.</summary>
    public const int MaxHeaders = 20;

    private static readonly HashSet<string> s_forbidden = new(StringComparer.OrdinalIgnoreCase)
    {
        "host", "content-length", "transfer-encoding", "connection", "keep-alive", "upgrade", "te", "trailer", "expect",
        "authorization", "proxy-authorization", "proxy-connection", "cookie", "set-cookie", "content-type",
    };

    /// <summary>Validates a header name and value.</summary>
    /// <param name="name">The name.</param>
    /// <param name="value">The value.</param>
    /// <returns>An error message or <see langword="null"/>.</returns>
    public static string? Validate(string? name, string? value)
    {
        if (string.IsNullOrWhiteSpace(name) || !HeaderNameRegex().IsMatch(name))
        {
            return $"'{name}' is not a valid header name.";
        }

        if (s_forbidden.Contains(name))
        {
            return $"The header '{name}' cannot be configured (use the authentication settings for credentials).";
        }

        if (value is null || value.Length > 2000 || value.Any(c => c is '\r' or '\n' or '\0'))
        {
            return $"The value of header '{name}' is invalid.";
        }

        return null;
    }

    /// <summary>Parses the default headers JSON object; invalid entries are skipped.</summary>
    /// <param name="json">The JSON.</param>
    /// <returns>The valid headers.</returns>
    public static IReadOnlyDictionary<string, string> Parse(string? json)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
        {
            return result;
        }

        try
        {
            var values = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
            foreach (var pair in values.Take(MaxHeaders))
            {
                if (Validate(pair.Key, pair.Value) is null)
                {
                    result[pair.Key] = pair.Value;
                }
            }
        }
        catch (JsonException)
        {
            // Invalid JSON yields no default headers.
        }

        return result;
    }

    [GeneratedRegex(@"^[A-Za-z0-9!#$%&'*+.^_`|~\-]{1,100}$", RegexOptions.CultureInvariant)]
    private static partial Regex HeaderNameRegex();
}
