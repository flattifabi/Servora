namespace Servora.Application.Security;

/// <summary>
/// Resolves effective permissions of users (direct roles + group roles) and API keys. Results are cached briefly and
/// invalidated whenever roles, groups or memberships change.
/// </summary>
public interface IPermissionService
{
    /// <summary>Gets the effective permission keys of a user.</summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The set of permission keys (may contain the wildcard <c>*</c>).</returns>
    Task<IReadOnlySet<string>> GetUserPermissionsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Gets the effective role names of a user.</summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The role names.</returns>
    Task<IReadOnlySet<string>> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Checks a single permission for a user.</summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="permission">The permission key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when granted.</returns>
    Task<bool> UserHasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken = default);

    /// <summary>Invalidates cached permissions for one user, or for everybody when <paramref name="userId"/> is null.</summary>
    /// <param name="userId">The user identifier or <see langword="null"/>.</param>
    void Invalidate(Guid? userId = null);
}
