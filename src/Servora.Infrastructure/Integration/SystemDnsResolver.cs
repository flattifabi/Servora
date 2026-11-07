using System.Net;
using System.Net.Sockets;

namespace Servora.Infrastructure.Integration;

/// <summary><see cref="IDnsResolver"/> using the operating system resolver.</summary>
public sealed class SystemDnsResolver : IDnsResolver
{
    /// <inheritdoc />
    public async Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        try
        {
            return await Dns.GetHostAddressesAsync(host, cancellationToken);
        }
        catch (SocketException)
        {
            return [];
        }
    }
}
