using Servora.Application.Security;

namespace Servora.Tests.Unit.Observability;

/// <summary><see cref="IPermissionService"/> with fixed role memberships for report sharing tests.</summary>
internal sealed class TestPermissionService : IPermissionService
{
    private readonly Dictionary<Guid, HashSet<string>> _roles = [];

    /// <summary>Assigns roles to a user.</summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="roles">The role names.</param>
    public void Assign(Guid userId, params string[] roles) => _roles[userId] = [.. roles];

    /// <inheritdoc />
    public Task<IReadOnlySet<string>> GetUserPermissionsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

    /// <inheritdoc />
    public Task<IReadOnlySet<string>> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlySet<string>>(_roles.TryGetValue(userId, out var roles) ? roles : new HashSet<string>());

    /// <inheritdoc />
    public Task<bool> UserHasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    /// <inheritdoc />
    public void Invalidate(Guid? userId = null)
    {
    }
}
