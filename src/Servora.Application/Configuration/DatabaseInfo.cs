namespace Servora.Application.Configuration;

/// <summary>
/// Information about the active database for the administration UI. Never contains credentials. Migration history and
/// backups are owned by the backup module (<c>/admin/backups</c>); only a short schema status is provided here.
/// </summary>
public sealed record DatabaseInfo
{
    /// <summary>Gets the provider key (<c>Sqlite</c>, <c>SqlServer</c>, <c>PostgreSql</c>).</summary>
    public required string ProviderKey { get; init; }

    /// <summary>Gets the provider display name.</summary>
    public required string ProviderName { get; init; }

    /// <summary>Gets the connection string with passwords masked.</summary>
    public required string RedactedConnectionString { get; init; }

    /// <summary>Gets where the configuration comes from (encrypted <c>database.json</c> or application configuration).</summary>
    public required string ConfigurationSource { get; init; }

    /// <summary>Gets the database name.</summary>
    public string? DatabaseName { get; init; }

    /// <summary>Gets the data source (server or file).</summary>
    public string? DataSource { get; init; }

    /// <summary>Gets the server version reported by the provider.</summary>
    public string? ServerVersion { get; init; }

    /// <summary>Gets a value indicating whether the schema is managed by migrations (otherwise created by the development schema builder).</summary>
    public bool UsesMigrations { get; init; }

    /// <summary>Gets the number of applied migrations.</summary>
    public int AppliedMigrations { get; init; }

    /// <summary>Gets the number of pending migrations.</summary>
    public int PendingMigrations { get; init; }

    /// <summary>Gets the database size in bytes where available (SQLite file size), otherwise <see langword="null"/>.</summary>
    public long? SizeBytes { get; init; }

    /// <summary>Gets an error when the database could not be inspected.</summary>
    public string? Error { get; init; }

    /// <summary>Gets a short human readable schema status.</summary>
    public string SchemaStatus => Error is not null
        ? "Unavailable"
        : !UsesMigrations
            ? "Development schema (no migrations)"
            : PendingMigrations > 0 ? $"{PendingMigrations} migration(s) pending" : "Up to date";
}
