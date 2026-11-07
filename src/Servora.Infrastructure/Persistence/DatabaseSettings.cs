namespace Servora.Infrastructure.Persistence;

/// <summary>The active database configuration (provider key and plain connection string, held in memory only).</summary>
/// <param name="Provider">The provider key, see <see cref="IDatabaseProvider.Key"/>.</param>
/// <param name="ConnectionString">The plain connection string.</param>
public sealed record DatabaseSettings(string Provider, string ConnectionString);
