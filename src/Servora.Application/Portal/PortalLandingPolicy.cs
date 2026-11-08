using Servora.Application.Security;

namespace Servora.Application.Portal;

/// <summary>
/// Decides where a signed-in user lands when opening <c>/</c>. "My Work" is the fulfiller workspace (incident, problem,
/// change and request queues); users who may use the self-service portal but cannot read any of these queues (for
/// example the built-in <c>user</c> role) land in the portal instead of an empty or denied workspace.
/// </summary>
public static class PortalLandingPolicy
{
    /// <summary>The portal home route.</summary>
    public const string PortalRoute = "/portal";

    /// <summary>The permissions that make the fulfiller workspace useful (any one of them keeps the user on My Work).</summary>
    public static IReadOnlyList<string> WorkspacePermissions { get; } =
    [
        Permissions.Incidents.Read,
        Permissions.Problems.Read,
        Permissions.Changes.Read,
        Permissions.Requests.Read,
    ];

    /// <summary>Checks whether a user with the given effective permissions should land in the portal.</summary>
    /// <param name="granted">The effective permissions of the user.</param>
    /// <returns><see langword="true"/> when the user holds <c>portal.access</c> but none of <see cref="WorkspacePermissions"/>.</returns>
    public static bool ShouldLandInPortal(IReadOnlySet<string> granted)
    {
        ArgumentNullException.ThrowIfNull(granted);
        return PermissionMatcher.IsGranted(granted, Permissions.Portal.Access)
            && !WorkspacePermissions.Any(permission => PermissionMatcher.IsGranted(granted, permission));
    }
}
