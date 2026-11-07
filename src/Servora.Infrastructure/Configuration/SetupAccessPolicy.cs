using System.Net;

namespace Servora.Infrastructure.Configuration;

/// <summary>
/// Decides whether a client connects from the local machine and may use the setup wizard without the setup token.
/// Requests that carry proxy forwarding headers are always treated as remote, because a reverse proxy on the same host
/// would otherwise make every client look local.
/// </summary>
public static class SetupAccessPolicy
{
    /// <summary>Header names that indicate a request was forwarded by a proxy.</summary>
    public static IReadOnlyList<string> ForwardingHeaders { get; } = ["X-Forwarded-For", "X-Forwarded-Host", "X-Real-IP", "Forwarded", "X-Original-For"];

    /// <summary>Checks whether a connection originates from the local machine.</summary>
    /// <param name="remoteAddress">The remote address of the connection.</param>
    /// <param name="localAddress">The local address of the connection.</param>
    /// <param name="hasForwardingHeaders">Whether the request carries proxy forwarding headers.</param>
    /// <returns><see langword="true"/> for local clients.</returns>
    public static bool IsLocalClient(IPAddress? remoteAddress, IPAddress? localAddress, bool hasForwardingHeaders)
    {
        if (hasForwardingHeaders || remoteAddress is null)
        {
            return false;
        }

        var remote = remoteAddress.IsIPv4MappedToIPv6 ? remoteAddress.MapToIPv4() : remoteAddress;
        if (IPAddress.IsLoopback(remote))
        {
            return true;
        }

        if (localAddress is null)
        {
            return false;
        }

        var local = localAddress.IsIPv4MappedToIPv6 ? localAddress.MapToIPv4() : localAddress;
        return remote.Equals(local);
    }
}
