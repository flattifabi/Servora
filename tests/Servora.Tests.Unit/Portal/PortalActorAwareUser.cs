using Servora.Application.Common;
using Servora.Application.Security;

namespace Servora.Tests.Unit.Portal;

/// <summary>
/// Scoped test actor that behaves like the production <c>CurrentUser</c>: an actor set on the scope's
/// <see cref="ActorContext"/> takes precedence (with its restricted permissions), otherwise the shared
/// <see cref="PortalTestUser"/> answers.
/// </summary>
/// <param name="context">The scope's actor context.</param>
/// <param name="user">The switchable test user.</param>
public sealed class PortalActorAwareUser(ActorContext context, PortalTestUser user) : ICurrentUser
{
    /// <inheritdoc />
    public Guid? UserId => context.Override is { } actor ? actor.UserId : user.UserId;

    /// <inheritdoc />
    public string Name => context.Override?.Name ?? user.Name;

    /// <inheritdoc />
    public string DisplayName => context.Override?.DisplayName ?? user.DisplayName;

    /// <inheritdoc />
    public ActorType ActorType => context.Override?.ActorType ?? user.ActorType;

    /// <inheritdoc />
    public Guid? ApiKeyId => null;

    /// <inheritdoc />
    public bool IsAuthenticated => true;

    /// <inheritdoc />
    public string? IpAddress => null;

    /// <inheritdoc />
    public string? CorrelationId => context.CorrelationId;

    /// <inheritdoc />
    public Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken = default) =>
        context.Override is { } actor
            ? Task.FromResult(actor.Permissions is null ? actor.ActorType == ActorType.System : PermissionMatcher.IsGranted(actor.Permissions, permission))
            : user.HasPermissionAsync(permission, cancellationToken);
}
