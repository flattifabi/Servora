using System.ComponentModel;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Servora.Application.Security;

/// <summary>Discovers all permission keys declared in <see cref="Permissions"/> for role editors and validation.</summary>
/// <remarks>
/// Besides the declared keys, two dynamic families are known, matching how they are checked at runtime
/// (<see cref="PermissionMatcher"/> with prefix wildcards):
/// <list type="bullet">
/// <item><c>table.&lt;table&gt;.&lt;read|create|write|delete&gt;</c> – generic table permissions checked by the record
/// service (<c>TablePolicies.GenericPermission</c>), plus the wildcards <c>table.*</c> and <c>table.&lt;table&gt;.*</c>.</item>
/// <item><c>mcp.tool.&lt;tool&gt;</c> – MCP tool gates, plus <c>mcp.tool.*</c>.</item>
/// </list>
/// </remarks>
public static partial class PermissionCatalog
{
    /// <summary>Prefix of the dynamic generic table permissions.</summary>
    public const string TablePrefix = "table.";

    /// <summary>Prefix of the dynamic MCP tool permissions.</summary>
    public const string McpToolPrefix = "mcp.tool.";

    private static readonly string[] s_tableOperations = ["read", "create", "write", "delete"];

    private static readonly Lazy<IReadOnlyList<PermissionDefinition>> s_all = new(Discover);

    /// <summary>Gets all declared permissions ordered by group and key.</summary>
    public static IReadOnlyList<PermissionDefinition> All => s_all.Value;

    /// <summary>Checks whether a key is a declared permission, a valid prefix wildcard or the global wildcard.</summary>
    /// <param name="key">The key to validate.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    public static bool IsKnown(string key)
    {
        if (key == Permissions.All)
        {
            return true;
        }

        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        if (key.StartsWith(TablePrefix, StringComparison.Ordinal))
        {
            return IsKnownTablePermission(key[TablePrefix.Length..]);
        }

        if (key.StartsWith(McpToolPrefix, StringComparison.Ordinal))
        {
            var tool = key[McpToolPrefix.Length..];
            return tool == "*" || ToolName().IsMatch(tool);
        }

        if (key.EndsWith(".*", StringComparison.Ordinal))
        {
            var prefix = key[..^1];
            return All.Any(p => p.Key.StartsWith(prefix, StringComparison.Ordinal));
        }

        return All.Any(p => p.Key == key);
    }

    // "*", "<table>.*" or "<table>.<operation>" (the part after "table.").
    private static bool IsKnownTablePermission(string rest)
    {
        if (rest == "*")
        {
            return true;
        }

        var dot = rest.LastIndexOf('.');
        if (dot <= 0)
        {
            return false;
        }

        var table = rest[..dot];
        var operation = rest[(dot + 1)..];
        return TableName().IsMatch(table) && (operation == "*" || s_tableOperations.Contains(operation));
    }

    private static IReadOnlyList<PermissionDefinition> Discover()
    {
        var result = new List<PermissionDefinition>();
        foreach (var nested in typeof(Permissions).GetNestedTypes(BindingFlags.Public | BindingFlags.Static))
        {
            var group = nested.GetCustomAttribute<DescriptionAttribute>()?.Description ?? nested.Name;
            foreach (var field in nested.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
            {
                if (field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
                {
                    var key = (string)field.GetRawConstantValue()!;
                    var description = field.GetCustomAttribute<DescriptionAttribute>()?.Description ?? key;
                    result.Add(new PermissionDefinition(key, group, description));
                }
            }
        }

        return result.OrderBy(p => p.Group).ThenBy(p => p.Key).ToList();
    }

    [GeneratedRegex("^[a-z][a-z0-9_]{0,79}$", RegexOptions.CultureInvariant)]
    private static partial Regex TableName();

    [GeneratedRegex("^[a-z][a-z0-9_]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex ToolName();
}
