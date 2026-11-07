using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servora.Application.Common;
using Servora.Domain.Integration;

namespace Servora.Application.Integration.DataSources;

/// <summary>Singleton <see cref="INetworkAllowListProvider"/> that caches the enabled allow-list entries for one minute.</summary>
/// <param name="scopeFactory">The scope factory (the database factory is scoped).</param>
public sealed class NetworkAllowListProvider(IServiceScopeFactory scopeFactory) : INetworkAllowListProvider
{
    private static readonly TimeSpan s_cacheDuration = TimeSpan.FromMinutes(1);
    private IReadOnlyList<IPNetwork>? _cached;
    private DateTimeOffset _loadedAt;

    /// <summary>Parses an allow-list entry (CIDR or single address).</summary>
    /// <param name="text">The text.</param>
    /// <param name="network">The network.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    public static bool TryParse(string? text, out IPNetwork network)
    {
        network = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (!trimmed.Contains('/') && IPAddress.TryParse(trimmed, out var single))
        {
            network = new IPNetwork(single, single.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128);
            return true;
        }

        return IPNetwork.TryParse(trimmed, out network);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IPNetwork>> GetAllowedNetworksAsync(CancellationToken cancellationToken = default)
    {
        var cached = Volatile.Read(ref _cached);
        if (cached is not null && DateTimeOffset.UtcNow - _loadedAt < s_cacheDuration)
        {
            return cached;
        }

        using var scope = scopeFactory.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IAppDbContextFactory>();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entries = await db.Set<NetworkAllowListEntry>().AsNoTracking()
            .Where(e => e.IsEnabled)
            .OrderBy(e => e.Cidr)
            .Select(e => e.Cidr)
            .Take(500)
            .ToListAsync(cancellationToken);
        var networks = new List<IPNetwork>();
        foreach (var entry in entries)
        {
            if (TryParse(entry, out var network))
            {
                networks.Add(network);
            }
        }

        _loadedAt = DateTimeOffset.UtcNow;
        Volatile.Write(ref _cached, networks);
        return networks;
    }

    /// <inheritdoc />
    public void Invalidate() => Volatile.Write(ref _cached, null);
}
