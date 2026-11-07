using Servora.Application.Common;
using Servora.Application.Security;

namespace Servora.Tests.Unit.Api.Fakes;

/// <summary>
/// Configurable <see cref="ICurrentUser"/>. When an <see cref="ActorContext"/> with an override is supplied, the
/// override wins (same resolution order as the production implementation).
/// </summary>
/// <param name="actorContext">An optional actor context.</param>
internal sealed class TestCurrentUser(ActorContext? actorContext = null) : ICurrentUser
{
    private Guid? _userId = Guid.CreateVersion7();

    /// <summary>Gets the granted permission keys (wildcards supported).</summary>
    public HashSet<string> Permissions { get; } = [Servora.Application.Security.Permissions.All];

    /// <inheritdoc />
    public Guid? UserId
    {
        get => actorContext?.Override is { } actor ? actor.UserId : _userId;
        set => _userId = value;
    }

    /// <inheritdoc />
    public string Name => actorContext?.Override?.Name ?? "tester";

    /// <inheritdoc />
    public string DisplayName => actorContext?.Override?.DisplayName ?? "Test User";

    /// <inheritdoc />
    public ActorType ActorType => actorContext?.Override?.ActorType ?? ActorType.User;

    /// <inheritdoc />
    public Guid? ApiKeyId => null;

    /// <inheritdoc />
    public bool IsAuthenticated => true;

    /// <inheritdoc />
    public string? IpAddress => null;

    /// <inheritdoc />
    public string? CorrelationId => actorContext?.CorrelationId;

    /// <summary>Replaces the granted permissions.</summary>
    /// <param name="permissions">The new permission keys.</param>
    public void Grant(params string[] permissions)
    {
        Permissions.Clear();
        Permissions.UnionWith(permissions);
    }

    /// <inheritdoc />
    public Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken = default)
    {
        if (actorContext?.Override is { } actor)
        {
            return Task.FromResult(actor.Permissions is not null && PermissionMatcher.IsGranted(actor.Permissions, permission));
        }

        return Task.FromResult(PermissionMatcher.IsGranted(Permissions, permission));
    }
}
