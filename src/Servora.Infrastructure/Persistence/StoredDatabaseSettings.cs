namespace Servora.Infrastructure.Persistence;

/// <summary>On-disk representation of the database settings (<c>database.json</c>).</summary>
/// <param name="Provider">The provider key.</param>
/// <param name="ProtectedConnectionString">The connection string encrypted with Data Protection.</param>
internal sealed record StoredDatabaseSettings(string Provider, string ProtectedConnectionString);
