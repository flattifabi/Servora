using Servora.Application.Security;

namespace Servora.Application.Identity;

/// <summary>
/// Prevents privilege escalation in identity administration: an actor can only grant permissions (directly, through a
/// role, a group or a directory mapping) that the actor holds itself, and can only manage accounts whose effective
/// permissions are fully covered by the actor's own permissions.
/// </summary>
/// <remarks>
/// Coverage uses the same semantics as <see cref="PermissionMatcher"/>: the global wildcard <c>*</c> is only covered by
/// <c>*</c>, a prefix wildcard such as <c>incident.*</c> is covered by <c>incident.*</c> or a broader prefix wildcard,
/// and a literal key is covered by itself or any matching wildcard.
/// </remarks>
public static class PrivilegeEscalationGuard
{
    /// <summary>Returns the requested permission keys that are not covered by the actor's permissions.</summary>
    /// <param name="actorPermissions">The actor's effective permission keys.</param>
    /// <param name="requested">The permission keys the actor wants to grant.</param>
    /// <returns>The uncovered keys (distinct, ordinal), empty when the grant is allowed.</returns>
    public static IReadOnlyList<string> FindUngranted(IReadOnlySet<string> actorPermissions, IEnumerable<string> requested)
    {
        ArgumentNullException.ThrowIfNull(actorPermissions);
        ArgumentNullException.ThrowIfNull(requested);
        return requested
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.Ordinal)
            .Where(k => !PermissionMatcher.IsGranted(actorPermissions, k))
            .ToList();
    }

    /// <summary>
    /// Returns the requested permission keys the actor does not hold, evaluating each key with a permission check
    /// delegate (typically <c>ICurrentUser.HasPermissionAsync</c>, which also handles API key scopes and system actors).
    /// </summary>
    /// <param name="holds">Delegate that checks whether the actor holds a permission key.</param>
    /// <param name="requested">The permission keys the actor wants to grant.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The uncovered keys, empty when the grant is allowed.</returns>
    public static async Task<IReadOnlyList<string>> FindUngrantedAsync(
        Func<string, CancellationToken, Task<bool>> holds,
        IEnumerable<string> requested,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(holds);
        ArgumentNullException.ThrowIfNull(requested);
        var missing = new List<string>();
        foreach (var key in requested.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct(StringComparer.Ordinal))
        {
            if (!await holds(key, cancellationToken))
            {
                missing.Add(key);
            }
        }

        return missing;
    }

    /// <summary>Builds the user-safe error message for a denied grant.</summary>
    /// <param name="missing">The permission keys the actor does not hold.</param>
    /// <returns>The message.</returns>
    public static string Describe(IReadOnlyList<string> missing)
    {
        var shown = string.Join(", ", missing.Take(5));
        var more = missing.Count > 5 ? $" and {missing.Count - 5} more" : string.Empty;
        return $"You can only grant permissions you hold yourself. Missing: {shown}{more}.";
    }
}
