using System.Net;

namespace Servora.Application.Integration.DataSources;

/// <summary>Provides the administrator-approved network ranges the SSRF guard may connect to despite being restricted.</summary>
public interface INetworkAllowListProvider
{
    /// <summary>Gets the enabled allow-list ranges (cached).</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The ranges.</returns>
    Task<IReadOnlyList<IPNetwork>> GetAllowedNetworksAsync(CancellationToken cancellationToken = default);

    /// <summary>Clears the cache after the allow-list changed.</summary>
    void Invalidate();
}
