using Servora.Application.Common;
using Servora.Application.Security;

namespace Servora.Tests.Unit.Observability;

/// <summary><see cref="ICurrentUser"/> with a fixed permission set for service tests.</summary>
internal sealed class TestCurrentUser(params string[] permissions) : ICurrentUser
{
    private readonly HashSet<string> _permissions = [.. permissions];

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
    public string? IpAddress => "127.0.0.1";

    /// <inheritdoc />
    public string? CorrelationId => "test-correlation";

    /// <inheritdoc />
    public Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken = default) =>
        Task.FromResult(PermissionMatcher.IsGranted(_permissions, permission));
}
