using System.Collections.Concurrent;
using Servora.Application.Security;

namespace Servora.Tests.Unit.Automation.Fakes;

/// <summary>Unencrypted in-memory <see cref="ISecretStore"/> for tests.</summary>
internal sealed class InMemorySecretStore : ISecretStore
{
    private readonly ConcurrentDictionary<string, string> _values = new(StringComparer.Ordinal);

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
        _values.TryRemove(name, out _);
        return Task.CompletedTask;
    }
}
