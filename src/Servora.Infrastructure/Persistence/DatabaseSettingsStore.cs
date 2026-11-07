using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Servora.Application.Common;

namespace Servora.Infrastructure.Persistence;

/// <summary>
/// Loads and saves the database configuration in <c>&lt;data dir&gt;/database.json</c>. The connection string is
/// encrypted with ASP.NET Core Data Protection because it may contain credentials. A configuration section
/// <c>Servora:Database</c> (Provider / ConnectionString) can pre-configure the database for development, containers
/// or unattended installs, in which case the setup wizard skips the database step.
/// </summary>
public sealed class DatabaseSettingsStore
{
    private const string Purpose = "Servora.DatabaseSettings.v1";
    private readonly string _filePath;
    private readonly IDataProtector _protector;
    private readonly object _sync = new();
    private DatabaseSettings? _current;
    private int _version;

    /// <summary>Initializes a new instance of the <see cref="DatabaseSettingsStore"/> class.</summary>
    /// <param name="paths">The application paths.</param>
    /// <param name="dataProtection">The data protection provider.</param>
    /// <param name="configuration">The application configuration.</param>
    public DatabaseSettingsStore(IAppPaths paths, IDataProtectionProvider dataProtection, IConfiguration configuration)
    {
        _filePath = Path.Combine(paths.DataDirectory, "database.json");
        _protector = dataProtection.CreateProtector(Purpose);
        _current = Load(configuration);
    }

    /// <summary>Raised after the settings changed.</summary>
    public event Action? Changed;

    /// <summary>Gets a value indicating whether a database is configured.</summary>
    public bool IsConfigured => Current is not null;

    /// <summary>Gets the current settings or <see langword="null"/> when not configured.</summary>
    public DatabaseSettings? Current
    {
        get
        {
            lock (_sync)
            {
                return _current;
            }
        }
    }

    /// <summary>Gets a number that increments on every change (used to cache EF options).</summary>
    public int Version => Volatile.Read(ref _version);

    /// <summary>Persists new settings (encrypted) and activates them.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the file is written.</returns>
    public async Task SaveAsync(DatabaseSettings settings, CancellationToken cancellationToken = default)
    {
        var document = new StoredDatabaseSettings(settings.Provider, _protector.Protect(settings.ConnectionString));
        var json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
        var temp = _filePath + ".tmp";
        await File.WriteAllTextAsync(temp, json, cancellationToken);
        File.Move(temp, _filePath, overwrite: true);
        lock (_sync)
        {
            _current = settings;
        }

        Interlocked.Increment(ref _version);
        Changed?.Invoke();
    }

    private DatabaseSettings? Load(IConfiguration configuration)
    {
        if (File.Exists(_filePath))
        {
            var document = JsonSerializer.Deserialize<StoredDatabaseSettings>(File.ReadAllText(_filePath));
            if (document is not null && !string.IsNullOrWhiteSpace(document.Provider) && !string.IsNullOrWhiteSpace(document.ProtectedConnectionString))
            {
                return new DatabaseSettings(document.Provider, _protector.Unprotect(document.ProtectedConnectionString));
            }
        }

        var provider = configuration["Servora:Database:Provider"];
        var connectionString = configuration["Servora:Database:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(provider) && !string.IsNullOrWhiteSpace(connectionString))
        {
            return new DatabaseSettings(provider, connectionString.Replace("{DataDirectory}", Path.GetDirectoryName(_filePath), StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }
}
