namespace Servora.Application.Backup;

/// <summary>Migration state of the configured database.</summary>
/// <param name="Provider">The database provider key.</param>
/// <param name="UsesMigrations">Whether this version ships migrations (always true for official builds; a build without migrations cannot start).</param>
/// <param name="CurrentMigration">The last applied migration.</param>
/// <param name="Applied">The applied migrations, oldest first.</param>
/// <param name="Pending">The migrations that will be applied at the next start, oldest first.</param>
/// <param name="KnownMigrations">The number of migrations known to this version.</param>
/// <param name="Problem">A user-safe problem description (for example a database newer than this version).</param>
public sealed record DatabaseSchemaStatus(
    string Provider,
    bool UsesMigrations,
    string? CurrentMigration,
    IReadOnlyList<string> Applied,
    IReadOnlyList<string> Pending,
    int KnownMigrations,
    string? Problem);
