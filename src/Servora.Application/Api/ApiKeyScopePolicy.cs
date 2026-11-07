using System.Text.RegularExpressions;
using Servora.Application.Common;
using Servora.Application.Mcp;
using Servora.Application.Security;

namespace Servora.Application.Api;

/// <summary>
/// Rules for API key scopes: scopes are permission keys from the catalog (plus <c>mcp.tool.*</c> scopes) and a creator
/// can only grant scopes that are covered by the creator's own permissions (no privilege escalation through keys).
/// </summary>
public static partial class ApiKeyScopePolicy
{
    /// <summary>The maximum number of scopes per key.</summary>
    public const int MaxScopes = 200;

    private static readonly char[] s_separators = [' ', ',', ';', '\n', '\r', '\t'];

    /// <summary>Parses a stored or entered scope list.</summary>
    /// <param name="scopes">The scopes separated by whitespace, commas or semicolons.</param>
    /// <returns>The distinct scopes in ordinal order.</returns>
    public static IReadOnlyList<string> Parse(string? scopes) =>
        string.IsNullOrWhiteSpace(scopes)
            ? []
            : scopes.Split(s_separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

    /// <summary>Formats scopes for storage.</summary>
    /// <param name="scopes">The scopes.</param>
    /// <returns>The space-separated list.</returns>
    public static string Format(IEnumerable<string> scopes) => string.Join(' ', scopes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));

    /// <summary>Checks whether a creator holding <paramref name="creatorPermissions"/> may grant a scope.</summary>
    /// <param name="creatorPermissions">The creator's effective permissions.</param>
    /// <param name="scope">The scope to grant.</param>
    /// <returns><see langword="true"/> when the scope is known and covered by the creator's permissions.</returns>
    public static bool IsGrantable(IReadOnlySet<string> creatorPermissions, string scope)
    {
        if (string.IsNullOrEmpty(scope) || scope.Length > 128 || !ScopeSyntax().IsMatch(scope))
        {
            return false;
        }

        if (scope.StartsWith(McpScopes.ToolPrefix, StringComparison.Ordinal))
        {
            // Tool scopes are gates on top of the underlying permissions, which are always checked as well.
            var toolName = scope[McpScopes.ToolPrefix.Length..];
            return (toolName == "*" || ToolNameSyntax().IsMatch(toolName)) && PermissionMatcher.IsGranted(creatorPermissions, Permissions.Mcp.Access);
        }

        return PermissionCatalog.IsKnown(scope) && PermissionMatcher.IsGranted(creatorPermissions, scope);
    }

    /// <summary>Validates a requested scope list against the creator's permissions.</summary>
    /// <param name="creatorPermissions">The creator's effective permissions.</param>
    /// <param name="requested">The requested scopes.</param>
    /// <returns>The normalized scopes or a validation / authorization error.</returns>
    public static Result<IReadOnlyList<string>> Validate(IReadOnlySet<string> creatorPermissions, IEnumerable<string> requested)
    {
        var scopes = Parse(string.Join(' ', requested));
        if (scopes.Count == 0)
        {
            return Result<IReadOnlyList<string>>.Invalid("Select at least one scope.");
        }

        if (scopes.Count > MaxScopes)
        {
            return Result<IReadOnlyList<string>>.Invalid($"At most {MaxScopes} scopes are allowed.");
        }

        var rejected = scopes.Where(s => !IsGrantable(creatorPermissions, s)).ToList();
        if (rejected.Count > 0)
        {
            return Result<IReadOnlyList<string>>.Forbidden(
                $"You cannot grant these scopes because they are unknown or exceed your own permissions: {string.Join(", ", rejected.Take(10))}.");
        }

        return Result<IReadOnlyList<string>>.Success(scopes);
    }

    [GeneratedRegex("^[a-z0-9_.*]+$", RegexOptions.CultureInvariant)]
    private static partial Regex ScopeSyntax();

    [GeneratedRegex("^[a-z][a-z0-9_]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex ToolNameSyntax();
}
