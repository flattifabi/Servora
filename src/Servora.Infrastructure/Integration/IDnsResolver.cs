using System.Net;

namespace Servora.Infrastructure.Integration;

/// <summary>Resolves host names (abstraction for SSRF tests).</summary>
public interface IDnsResolver
{
    /// <summary>Resolves a host name to its addresses.</summary>
    /// <param name="host">The host name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The addresses (empty when the name does not resolve).</returns>
    Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken);
}
