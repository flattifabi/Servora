using Servora.Application.Security;

namespace Servora.Application.Ai;

/// <summary>
/// Computes the effective permissions of an agent identity: the intersection of the agent's scope and the requesting
/// user's permissions, wildcard-aware (<c>*</c>, <c>prefix.*</c>). The result never grants anything the user lacks.
/// </summary>
public static class AiPermissionIntersection
{
    /// <summary>Intersects an agent scope with user permissions.</summary>
    /// <param name="agentScope">The agent's maximum permissions.</param>
    /// <param name="userPermissions">The user's effective permissions.</param>
    /// <returns>The effective agent permissions.</returns>
    public static IReadOnlySet<string> Intersect(IEnumerable<string> agentScope, IReadOnlySet<string> userPermissions)
    {
        var scope = agentScope.ToHashSet(StringComparer.Ordinal);
        var result = new HashSet<string>(StringComparer.Ordinal);

        // Agent scope entries the user fully holds (including wildcards the user holds at least as broadly).
        foreach (var entry in scope.Where(e => PermissionMatcher.IsGranted(userPermissions, e)))
        {
            result.Add(entry);
        }

        // Concrete user permissions that fall under a wildcard of the agent scope.
        foreach (var permission in userPermissions.Where(p => p != Permissions.All && PermissionMatcher.IsGranted(scope, p)))
        {
            result.Add(permission);
        }

        return result;
    }
}
