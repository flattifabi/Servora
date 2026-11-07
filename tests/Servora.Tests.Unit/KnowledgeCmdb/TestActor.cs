using Servora.Application.Common;
using Servora.Application.Security;

namespace Servora.Tests.Unit.KnowledgeCmdb;

/// <summary>Configurable <see cref="ICurrentUser"/> for service tests.</summary>
/// <param name="displayName">The display name.</param>
/// <param name="permissions">The granted permissions (supports wildcards).</param>
public sealed class TestActor(string displayName, params string[] permissions) : ICurrentUser
{
    /// <summary>Gets or sets the granted permissions.</summary>
    public HashSet<string> Permissions { get; set; } = [.. permissions];

    /// <inheritdoc />
    public Guid? UserId { get; set; } = Guid.NewGuid();

    /// <inheritdoc />
    public string Name => displayName.ToLowerInvariant().Replace(' ', '.');

    /// <inheritdoc />
    public string DisplayName => displayName;

    /// <inheritdoc />
    public ActorType ActorType => ActorType.User;

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
        Task.FromResult(PermissionMatcher.IsGranted(Permissions, permission));
}
