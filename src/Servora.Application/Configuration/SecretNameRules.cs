using System.Text.RegularExpressions;

namespace Servora.Application.Configuration;

/// <summary>Naming and size rules for stored secrets.</summary>
public static partial class SecretNameRules
{
    /// <summary>The maximum length of a secret value.</summary>
    public const int MaxValueLength = 8000;

    /// <summary>The maximum length of a description.</summary>
    public const int MaxDescriptionLength = 500;

    /// <summary>Secrets older than this many days are reported as stale.</summary>
    public const int StaleAfterDays = 180;

    private static readonly string[] s_systemPrefixes = ["email.", "ldap.", "ai.", "datasource.", "integration.", "mcp."];

    /// <summary>Checks whether a secret name is valid: lower-case letters, digits, dot, dash and underscore, 3-200 characters, starting with a letter.</summary>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    public static bool IsValidName(string? name) => name is not null && NamePattern().IsMatch(name);

    /// <summary>Checks whether a secret belongs to a built-in feature (deleting it may break that feature).</summary>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true"/> for system secrets.</returns>
    public static bool IsSystemSecret(string name) => s_systemPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal));

    [GeneratedRegex("^[a-z][a-z0-9._-]{2,199}$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();
}
