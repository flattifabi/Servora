namespace Servora.Application.Common;

/// <summary>
/// The principal on whose behalf the current operation runs. Works for HTTP requests, Blazor circuits,
/// API key / MCP calls and background processing (where it represents the system or an impersonated actor).
/// </summary>
public interface ICurrentUser
{
    /// <summary>Gets the user identifier, or <see langword="null"/> when not a user (anonymous, system, pure API key).</summary>
    Guid? UserId { get; }

    /// <summary>Gets the login / technical name of the actor.</summary>
    string Name { get; }

    /// <summary>Gets the display name of the actor used in audit fields and activity streams.</summary>
    string DisplayName { get; }

    /// <summary>Gets the actor type.</summary>
    ActorType ActorType { get; }

    /// <summary>Gets the API key identifier when <see cref="ActorType"/> is <see cref="Common.ActorType.ApiKey"/>.</summary>
    Guid? ApiKeyId { get; }

    /// <summary>Gets a value indicating whether a principal is authenticated (including system actors).</summary>
    bool IsAuthenticated { get; }

    /// <summary>Gets the client IP address for interactive and API requests.</summary>
    string? IpAddress { get; }

    /// <summary>Gets the correlation identifier of the current request or execution.</summary>
    string? CorrelationId { get; }

    /// <summary>Checks whether the actor holds a permission. Always evaluated server-side.</summary>
    /// <param name="permission">The permission key, for example <c>incident.write</c>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when granted.</returns>
    Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken = default);
}
