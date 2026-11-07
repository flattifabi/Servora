using Microsoft.EntityFrameworkCore;
using Servora.Infrastructure.Backup;

namespace Servora.Tests.Unit.Backup.Infrastructure;

/// <summary><see cref="IBackupDatabase"/> backed by a context factory (test models on temporary SQLite files).</summary>
/// <param name="factory">Creates a context.</param>
internal sealed class TestBackupDatabase(Func<DbContext> factory) : IBackupDatabase
{
    /// <inheritdoc />
    public bool IsConfigured => true;

    /// <inheritdoc />
    public string ProviderKey => "Sqlite";

    /// <inheritdoc />
    public DbContext CreateContext() => factory();
}
