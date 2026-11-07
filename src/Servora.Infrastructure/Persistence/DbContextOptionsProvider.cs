using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Servora.Infrastructure.Persistence;

/// <summary>
/// Builds and caches <see cref="DbContextOptions{TContext}"/> for the currently configured database. Options are
/// rebuilt automatically when <see cref="DatabaseSettingsStore"/> changes (for example after the setup wizard).
/// </summary>
/// <param name="settingsStore">The settings store.</param>
/// <param name="providers">The provider registry.</param>
public sealed class DbContextOptionsProvider(DatabaseSettingsStore settingsStore, DatabaseProviderRegistry providers)
{
    private readonly object _sync = new();
    private DbContextOptions<ServoraDbContext>? _options;
    private int _version = -1;

    /// <summary>Gets the options for the active database.</summary>
    /// <returns>The options.</returns>
    /// <exception cref="DatabaseNotConfiguredException">Thrown when no database is configured yet (setup mode).</exception>
    public DbContextOptions<ServoraDbContext> GetOptions()
    {
        var version = settingsStore.Version;
        lock (_sync)
        {
            if (_options is not null && _version == version)
            {
                return _options;
            }

            var settings = settingsStore.Current ?? throw new DatabaseNotConfiguredException();
            _options = Build(settings);
            _version = version;
            return _options;
        }
    }

    /// <summary>Builds options for explicit settings (used by the setup wizard before activation).</summary>
    /// <param name="settings">The settings.</param>
    /// <returns>The options.</returns>
    public DbContextOptions<ServoraDbContext> Build(DatabaseSettings settings)
    {
        var builder = new DbContextOptionsBuilder<ServoraDbContext>();
        providers.Get(settings.Provider).Configure(builder, settings.ConnectionString);
        builder.ReplaceService<IModelCacheKeyFactory, ProviderAwareModelCacheKeyFactory>();
        builder.EnableDetailedErrors();
        return builder.Options;
    }
}
