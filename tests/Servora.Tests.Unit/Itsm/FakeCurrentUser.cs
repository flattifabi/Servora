using Servora.Application.Common;
using Servora.Application.Security;

namespace Servora.Tests.Unit.Itsm;

/// <summary>Configurable <see cref="ICurrentUser"/> for service tests.</summary>
internal sealed class FakeCurrentUser : ICurrentUser
{
    /// <summary>Gets the granted permission keys (wildcards supported).</summary>
    public HashSet<string> Permissions { get; } = [Servora.Application.Security.Permissions.All];

    /// <inheritdoc />
    public Guid? UserId { get; set; } = Guid.CreateVersion7();

    /// <inheritdoc />
    public string Name => "tester";

    /// <inheritdoc />
    public string DisplayName => "Test User";

    /// <inheritdoc />
    public ActorType ActorType => ActorType.User;

    /// <inheritdoc />
    public Guid? ApiKeyId => null;

    /// <inheritdoc />
    public bool IsAuthenticated => true;

    /// <inheritdoc />
    public string? IpAddress => null;

    /// <inheritdoc />
    public string? CorrelationId => null;

    /// <summary>Replaces the granted permissions.</summary>
    /// <param name="permissions">The new permission keys.</param>
    public void Grant(params string[] permissions)
    {
        Permissions.Clear();
        Permissions.UnionWith(permissions);
    }

    /// <inheritdoc />
    public Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken = default) =>
        Task.FromResult(PermissionMatcher.IsGranted(Permissions, permission));
}
