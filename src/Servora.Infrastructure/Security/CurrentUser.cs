using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Servora.Application.Common;
using Servora.Application.Security;

namespace Servora.Infrastructure.Security;

/// <summary>
/// Scoped <see cref="ICurrentUser"/>. Resolution order: explicit <see cref="ActorContext"/> override (system, agents),
/// HTTP request principal (API, MCP, prerendering), Blazor circuit principal.
/// </summary>
/// <param name="actorContext">The actor context.</param>
/// <param name="httpContextAccessor">The HTTP context accessor.</param>
/// <param name="circuitPrincipal">The circuit principal holder.</param>
/// <param name="services">The scoped service provider (the permission service is resolved lazily to avoid a DI cycle with the database factory).</param>
public sealed class CurrentUser(
    ActorContext actorContext,
    IHttpContextAccessor httpContextAccessor,
    CircuitPrincipalHolder circuitPrincipal,
    IServiceProvider services) : ICurrentUser
{
    /// <inheritdoc />
    public Guid? UserId => actorContext.Override is { } actor
        ? actor.UserId
        : Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <inheritdoc />
    public string Name => actorContext.Override?.Name ?? Principal?.Identity?.Name ?? "anonymous";

    /// <inheritdoc />
    public string DisplayName => actorContext.Override?.DisplayName
        ?? Principal?.FindFirstValue(ServoraClaimTypes.DisplayName)
        ?? Principal?.Identity?.Name
        ?? "Anonymous";

    /// <inheritdoc />
    public ActorType ActorType
    {
        get
        {
            if (actorContext.Override is { } actor)
            {
                return actor.ActorType;
            }

            if (Principal?.Identity?.IsAuthenticated != true)
            {
                return ActorType.Anonymous;
            }

            return Principal.FindFirstValue(ServoraClaimTypes.ActorType) switch
            {
                "apikey" => ActorType.ApiKey,
                "agent" => ActorType.Agent,
                _ => ActorType.User,
            };
        }
    }

    /// <inheritdoc />
    public Guid? ApiKeyId => Guid.TryParse(Principal?.FindFirstValue(ServoraClaimTypes.ApiKeyId), out var id) ? id : null;

    /// <inheritdoc />
    public bool IsAuthenticated => ActorType != ActorType.Anonymous;

    /// <inheritdoc />
    public string? IpAddress => httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? circuitPrincipal.IpAddress;

    /// <inheritdoc />
    public string? CorrelationId => actorContext.CorrelationId ?? httpContextAccessor.HttpContext?.TraceIdentifier;

    private ClaimsPrincipal? Principal
    {
        get
        {
            var httpUser = httpContextAccessor.HttpContext?.User;
            return httpUser?.Identity?.IsAuthenticated == true ? httpUser : circuitPrincipal.Principal ?? httpUser;
        }
    }

    /// <inheritdoc />
    public async Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken = default)
    {
        if (actorContext.Override is { } actor)
        {
            return actor.Permissions is null
                ? actor.ActorType == ActorType.System
                : PermissionMatcher.IsGranted(actor.Permissions, permission);
        }

        var principal = Principal;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (ActorType is ActorType.ApiKey or ActorType.Agent)
        {
            var scopes = principal.FindAll(ServoraClaimTypes.Permission).Select(c => c.Value).ToHashSet(StringComparer.Ordinal);
            if (!PermissionMatcher.IsGranted(scopes, permission))
            {
                return false;
            }

            // A key owned by a user can never exceed the owner's own permissions.
            return UserId is not { } ownerId || await services.GetRequiredService<IPermissionService>().UserHasPermissionAsync(ownerId, permission, cancellationToken);
        }

        return UserId is { } userId && await services.GetRequiredService<IPermissionService>().UserHasPermissionAsync(userId, permission, cancellationToken);
    }
}
