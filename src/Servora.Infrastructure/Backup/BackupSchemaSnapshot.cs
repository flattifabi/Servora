namespace Servora.Infrastructure.Backup;

/// <summary>The schema of a database at a migration, as read from the EF Core relational model.</summary>
/// <param name="Migration">The migration the schema corresponds to, or <see langword="null"/> without migration history.</param>
/// <param name="UsesMigrations">Whether the application ships migrations.</param>
/// <param name="Tables">The tables (without payload information) in foreign key order.</param>
public sealed record BackupSchemaSnapshot(string? Migration, bool UsesMigrations, IReadOnlyList<BackupTableManifest> Tables);
