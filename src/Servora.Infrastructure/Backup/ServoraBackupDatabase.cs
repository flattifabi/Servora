using Microsoft.EntityFrameworkCore;
using Servora.Infrastructure.Persistence;

namespace Servora.Infrastructure.Backup;

/// <summary><see cref="IBackupDatabase"/> for the configured Servora database.</summary>
/// <param name="settings">The database settings store.</param>
/// <param name="options">The context options provider.</param>
public sealed class ServoraBackupDatabase(DatabaseSettingsStore settings, DbContextOptionsProvider options) : IBackupDatabase
{
    /// <inheritdoc />
    public bool IsConfigured => settings.IsConfigured;

    /// <inheritdoc />
    public string ProviderKey => settings.Current?.Provider ?? throw new DatabaseNotConfiguredException();

    /// <inheritdoc />
    public DbContext CreateContext() => new ServoraDbContext(options.GetOptions());
}
