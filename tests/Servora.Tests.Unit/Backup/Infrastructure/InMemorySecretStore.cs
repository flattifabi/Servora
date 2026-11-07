using Servora.Application.Security;

namespace Servora.Tests.Unit.Backup.Infrastructure;

/// <summary>In-memory <see cref="ISecretStore"/>.</summary>
internal sealed class InMemorySecretStore : ISecretStore
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task SetAsync(string name, string plainValue, string? description = null, CancellationToken cancellationToken = default)
    {
        _values[name] = plainValue;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string?> GetAsync(string name, CancellationToken cancellationToken = default) =>
        Task.FromResult(_values.TryGetValue(name, out var value) ? value : null);

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(_values.ContainsKey(name));

    /// <inheritdoc />
    public Task DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        _values.Remove(name);
        return Task.CompletedTask;
    }
}
