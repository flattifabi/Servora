using Servora.Application.Security;

namespace Servora.Tests.Unit.Api.Fakes;

/// <summary>In-memory <see cref="IPermissionService"/> with per-user permission sets.</summary>
internal sealed class TestPermissionService : IPermissionService
{
    /// <summary>Gets the permissions per user.</summary>
    public Dictionary<Guid, HashSet<string>> Users { get; } = [];

    /// <summary>Sets the permissions of a user.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="permissions">The permission keys.</param>
    public void Set(Guid userId, params string[] permissions) => Users[userId] = permissions.ToHashSet(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<IReadOnlySet<string>> GetUserPermissionsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlySet<string>>(Users.TryGetValue(userId, out var set) ? set : new HashSet<string>());

    /// <inheritdoc />
    public Task<IReadOnlySet<string>> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

    /// <inheritdoc />
    public Task<bool> UserHasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.TryGetValue(userId, out var set) && PermissionMatcher.IsGranted(set, permission));

    /// <inheritdoc />
    public void Invalidate(Guid? userId = null)
    {
    }
}
