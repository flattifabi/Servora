using Servora.Application.Security;

namespace Servora.Tests.Unit.Api.Fakes;

/// <summary>Dictionary-backed <see cref="ISecretStore"/>.</summary>
internal sealed class InMemorySecretStore : ISecretStore
{
    /// <summary>Gets the stored secrets.</summary>
    public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task SetAsync(string name, string plainValue, string? description = null, CancellationToken cancellationToken = default)
    {
        Values[name] = plainValue;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string?> GetAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(Values.GetValueOrDefault(name));

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(Values.ContainsKey(name));

    /// <inheritdoc />
    public Task DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        Values.Remove(name);
        return Task.CompletedTask;
    }
}
