namespace Servora.Infrastructure.Persistence;

/// <summary>Thrown when database access is attempted before the setup wizard configured a database.</summary>
public sealed class DatabaseNotConfiguredException()
    : InvalidOperationException("No database is configured yet. Complete the setup wizard first.");
