using Microsoft.EntityFrameworkCore;

namespace Servora.Infrastructure.Backup;

/// <summary>
/// Creates bare EF Core contexts (no saving hooks, no record events) for backup, restore and migrations. Abstracted so
/// the engine can be tested against small test models with hand-written migrations.
/// </summary>
public interface IBackupDatabase
{
    /// <summary>Gets a value indicating whether a database is configured.</summary>
    bool IsConfigured { get; }

    /// <summary>Gets the provider key (<c>Sqlite</c>, <c>SqlServer</c>, <c>PostgreSql</c>).</summary>
    string ProviderKey { get; }

    /// <summary>Creates a new context. The caller disposes it.</summary>
    /// <returns>The context.</returns>
    DbContext CreateContext();
}
