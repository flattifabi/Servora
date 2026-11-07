using Microsoft.Extensions.Hosting;
using Servora.Infrastructure.Persistence;

namespace Servora.Infrastructure.Configuration;

/// <summary>
/// Writes the setup token file at startup while no database is configured, and removes a stale token file once the
/// instance is configured.
/// </summary>
/// <param name="settingsStore">The database settings store.</param>
/// <param name="tokenStore">The setup token store.</param>
public sealed class SetupTokenInitializer(DatabaseSettingsStore settingsStore, SetupTokenStore tokenStore) : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (settingsStore.IsConfigured)
        {
            tokenStore.Delete();
        }
        else
        {
            tokenStore.EnsureToken();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
