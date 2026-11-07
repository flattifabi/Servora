using Servora.Application.Common;

namespace Servora.Tests.Unit.Configuration;

/// <summary>Configurable <see cref="ICurrentUser"/> for service tests.</summary>
/// <param name="granted">Whether every permission is granted.</param>
internal sealed class TestCurrentUser(bool granted = true) : ICurrentUser
{
    /// <summary>Gets or sets a value indicating whether permissions are granted.</summary>
    public bool Granted { get; set; } = granted;

    /// <inheritdoc />
    public Guid? UserId { get; } = Guid.NewGuid();

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
    public string? CorrelationId => "test";

    /// <inheritdoc />
    public Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken = default) => Task.FromResult(Granted);
}
