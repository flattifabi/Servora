using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Servora.Application.Integration.DataSources;

namespace Servora.Infrastructure.Integration;

/// <summary>
/// Server-side request forgery protection for outbound data source requests. Validates URLs before sending (scheme,
/// no credentials, every resolved address allowed) and again at connect time through
/// <see cref="ConnectAsync"/>, which connects only to addresses it validated itself – so DNS rebinding between the check
/// and the connection cannot reach a blocked address. Redirect targets are validated by the invoker with
/// <see cref="ValidateUriAsync"/>.
/// </summary>
/// <param name="dns">The DNS resolver.</param>
/// <param name="allowList">The administrator allow-list.</param>
public sealed class SsrfGuard(IDnsResolver dns, INetworkAllowListProvider allowList)
{
    private const int MaxAddresses = 16;

    /// <summary>Validates a request URL.</summary>
    /// <param name="uri">The URL.</param>
    /// <param name="allowHttp">Whether plain HTTP is allowed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A user-safe error or <see langword="null"/> when the URL may be requested.</returns>
    public async Task<string?> ValidateUriAsync(Uri uri, bool allowHttp, CancellationToken cancellationToken)
    {
        if (!uri.IsAbsoluteUri)
        {
            return "Only absolute URLs can be requested.";
        }

        if (uri.Scheme != Uri.UriSchemeHttps && !(allowHttp && uri.Scheme == Uri.UriSchemeHttp))
        {
            return allowHttp ? "Only HTTP(S) URLs can be requested." : "Blocked by SSRF protection: only HTTPS URLs are allowed.";
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return "Blocked by SSRF protection: URLs with embedded credentials are not allowed.";
        }

        try
        {
            await ResolveAllowedAsync(uri.IdnHost, cancellationToken);
            return null;
        }
        catch (DataSourceBlockedException ex)
        {
            return ex.Message;
        }
    }

    /// <summary>Resolves a host and returns its addresses if every address is allowed.</summary>
    /// <param name="host">The host name or IP literal.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The allowed addresses.</returns>
    /// <exception cref="DataSourceBlockedException">The host does not resolve or resolves to a blocked address.</exception>
    public async Task<IReadOnlyList<IPAddress>> ResolveAllowedAsync(string host, CancellationToken cancellationToken)
    {
        var name = host.Trim().Trim('[', ']');
        if (name.Length == 0)
        {
            throw new DataSourceBlockedException("Blocked by SSRF protection: the URL has no host.");
        }

        IPAddress[] addresses;
        if (IPAddress.TryParse(name, out var literal))
        {
            addresses = [literal];
        }
        else
        {
            addresses = (await dns.ResolveAsync(name, cancellationToken)).Take(MaxAddresses).ToArray();
            if (addresses.Length == 0)
            {
                throw new DataSourceBlockedException($"The host '{name}' could not be resolved.");
            }
        }

        var allowed = await allowList.GetAllowedNetworksAsync(cancellationToken);
        foreach (var address in addresses)
        {
            if (NetworkAddressPolicy.GetBlockReason(address, allowed) is { } reason)
            {
                // Any blocked address rejects the host, so mixed public/private DNS answers cannot be abused.
                throw new DataSourceBlockedException($"Blocked by SSRF protection: '{name}' resolves to {address} ({reason}). An administrator can approve internal ranges in the network allow-list.");
            }
        }

        return addresses;
    }

    /// <summary>
    /// <see cref="SocketsHttpHandler.ConnectCallback"/> implementation: resolves and validates the host at connect time
    /// and connects directly to a validated address.
    /// </summary>
    /// <param name="context">The connection context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The connected network stream.</returns>
    public async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var addresses = await ResolveAllowedAsync(context.DnsEndPoint.Host, cancellationToken);
        Exception? last = null;
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (SocketException ex)
            {
                socket.Dispose();
                last = ex;
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        throw last ?? new SocketException((int)SocketError.HostUnreachable);
    }
}
