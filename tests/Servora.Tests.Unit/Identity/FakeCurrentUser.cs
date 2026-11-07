using Servora.Application.Common;
using Servora.Application.Security;

namespace Servora.Tests.Unit.Identity;

/// <summary>Test actor with a fixed permission set.</summary>
/// <param name="permissions">The granted permission keys.</param>
public sealed class FakeCurrentUser(params string[] permissions) : ICurrentUser
{
    private readonly HashSet<string> _permissions = new(permissions, StringComparer.Ordinal);

    /// <inheritdoc />
    public Guid? UserId { get; set; } = Guid.NewGuid();

    /// <inheritdoc />
    public string Name => "tester";

    /// <inheritdoc />
    public string DisplayName => "Test Administrator";

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
        Task.FromResult(PermissionMatcher.IsGranted(_permissions, permission));
}
