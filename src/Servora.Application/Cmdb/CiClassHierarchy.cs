using Servora.Domain.Cmdb;

namespace Servora.Application.Cmdb;

/// <summary>Pure helpers over the CI class hierarchy (inheritance chain, descendants, cycle detection).</summary>
public static class CiClassHierarchy
{
    private const int MaxDepth = 16;

    /// <summary>Gets the class and its ancestors, root first.</summary>
    /// <param name="classes">All classes.</param>
    /// <param name="key">The class key.</param>
    /// <returns>The chain (empty when the class is unknown).</returns>
    public static IReadOnlyList<CiClass> Chain(IReadOnlyCollection<CiClass> classes, string key)
    {
        var byKey = classes.ToDictionary(c => c.Key, StringComparer.Ordinal);
        var chain = new List<CiClass>();
        var current = key;
        while (current is not null && byKey.TryGetValue(current, out var cls) && chain.Count < MaxDepth && !chain.Contains(cls))
        {
            chain.Insert(0, cls);
            current = cls.ParentKey;
        }

        return chain;
    }

    /// <summary>Gets the effective attributes of a class (inherited first; a child attribute with the same name overrides).</summary>
    /// <param name="classes">All classes.</param>
    /// <param name="key">The class key.</param>
    /// <returns>The attributes.</returns>
    public static IReadOnlyList<CiAttributeDefinition> EffectiveAttributes(IReadOnlyCollection<CiClass> classes, string key)
    {
        var result = new List<CiAttributeDefinition>();
        foreach (var cls in Chain(classes, key))
        {
            foreach (var attribute in CiAttributeSchema.ParseDefinitions(cls.AttributesJson))
            {
                result.RemoveAll(a => a.Name == attribute.Name);
                result.Add(attribute);
            }
        }

        return result;
    }

    /// <summary>Gets the key of a class and the keys of all its descendants.</summary>
    /// <param name="classes">All classes.</param>
    /// <param name="key">The class key.</param>
    /// <returns>The keys.</returns>
    public static IReadOnlySet<string> SelfAndDescendants(IReadOnlyCollection<CiClass> classes, string key)
    {
        var result = new HashSet<string>(StringComparer.Ordinal) { key };
        var added = true;
        for (var round = 0; added && round < MaxDepth; round++)
        {
            added = false;
            foreach (var cls in classes)
            {
                if (cls.ParentKey is not null && result.Contains(cls.ParentKey) && result.Add(cls.Key))
                {
                    added = true;
                }
            }
        }

        return result;
    }

    /// <summary>Checks whether assigning <paramref name="parentKey"/> as parent of <paramref name="key"/> would create a cycle.</summary>
    /// <param name="classes">All classes.</param>
    /// <param name="key">The class key.</param>
    /// <param name="parentKey">The proposed parent key.</param>
    /// <returns><see langword="true"/> when a cycle would result.</returns>
    public static bool WouldCreateCycle(IReadOnlyCollection<CiClass> classes, string key, string? parentKey) =>
        parentKey is not null && (parentKey == key || SelfAndDescendants(classes, key).Contains(parentKey));
}
