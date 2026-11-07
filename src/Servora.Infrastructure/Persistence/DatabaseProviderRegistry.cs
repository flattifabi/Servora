namespace Servora.Infrastructure.Persistence;

/// <summary>Lookup of all registered <see cref="IDatabaseProvider"/> implementations.</summary>
/// <param name="providers">The registered providers.</param>
public sealed class DatabaseProviderRegistry(IEnumerable<IDatabaseProvider> providers)
{
    private readonly IReadOnlyList<IDatabaseProvider> _providers = providers.ToList();

    /// <summary>Gets all providers.</summary>
    public IReadOnlyList<IDatabaseProvider> All => _providers;

    /// <summary>Finds a provider by key.</summary>
    /// <param name="key">The provider key (case-insensitive).</param>
    /// <returns>The provider.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the provider is not registered.</exception>
    public IDatabaseProvider Get(string key) =>
        _providers.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Database provider '{key}' is not supported. Supported: {string.Join(", ", _providers.Select(p => p.Key))}.");
}
