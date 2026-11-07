using Servora.Application.Common;
using Servora.Application.Security;

namespace Servora.Tests.Unit.ServiceManagement;

/// <summary>Switchable test actor with an explicit permission set.</summary>
public sealed class FakeCurrentUser : ICurrentUser
{
    /// <summary>Gets or sets the granted permissions (supports <c>*</c> and <c>prefix.*</c>).</summary>
    public HashSet<string> Permissions { get; set; } = [Application.Security.Permissions.All];

    /// <inheritdoc />
    public Guid? UserId { get; set; }

    /// <inheritdoc />
    public string Name { get; set; } = "tester";

    /// <inheritdoc />
    public string DisplayName { get; set; } = "Test User";

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

    /// <summary>Switches the actor.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="displayName">The display name.</param>
    /// <param name="permissions">The permissions.</param>
    public void Become(Guid userId, string displayName, params string[] permissions)
    {
        UserId = userId;
        DisplayName = displayName;
        Name = displayName;
        Permissions = [.. permissions];
    }

    /// <inheritdoc />
    public Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken = default) =>
        Task.FromResult(PermissionMatcher.IsGranted(Permissions, permission));
}
