using System.ComponentModel;
using System.Reflection;

namespace Servora.Application.Security;

/// <summary>Discovers all permission keys declared in <see cref="Permissions"/> for role editors and validation.</summary>
public static class PermissionCatalog
{
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

        if (key.EndsWith(".*", StringComparison.Ordinal))
        {
            var prefix = key[..^1];
            return All.Any(p => p.Key.StartsWith(prefix, StringComparison.Ordinal));
        }

        return All.Any(p => p.Key == key) || key.StartsWith("table.", StringComparison.Ordinal) || key.StartsWith("mcp.tool.", StringComparison.Ordinal);
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
}
