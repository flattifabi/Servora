using Servora.Application.Common;
using Servora.Application.Security;

namespace Servora.Tests.Unit.Platform;

/// <summary>Test <see cref="ICurrentUser"/>: a user with the permissions of <see cref="TestIdentity"/>, or the explicit actor of the scope.</summary>
/// <param name="identity">The shared test identity.</param>
/// <param name="actorContext">The actor context of the scope.</param>
public sealed class TestCurrentUser(TestIdentity identity, ActorContext actorContext) : ICurrentUser
{
    /// <inheritdoc />
    public Guid? UserId => actorContext.Override is { } actor ? actor.UserId : identity.UserId;

    /// <inheritdoc />
    public string Name => actorContext.Override?.Name ?? "test";

    /// <inheritdoc />
    public string DisplayName => actorContext.Override?.DisplayName ?? identity.DisplayName;

    /// <inheritdoc />
    public ActorType ActorType => actorContext.Override?.ActorType ?? ActorType.User;

    /// <inheritdoc />
    public Guid? ApiKeyId => null;

    /// <inheritdoc />
    public bool IsAuthenticated => true;

    /// <inheritdoc />
    public string? IpAddress => "127.0.0.1";

    /// <inheritdoc />
    public string? CorrelationId => null;

    /// <inheritdoc />
    public Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken = default) =>
        Task.FromResult(actorContext.Override is { ActorType: ActorType.System, Permissions: null } || PermissionMatcher.IsGranted(identity.Permissions, permission));
}
