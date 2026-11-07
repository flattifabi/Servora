using System.Net;
using System.Net.Sockets;
using Servora.Installer.Abstractions;

namespace Servora.Installer.Platform;

/// <summary><see cref="IPortChecker"/> that tries to bind a TCP listener on all interfaces.</summary>
internal sealed class TcpPortChecker : IPortChecker
{
    /// <inheritdoc />
    public bool IsAvailable(int port)
    {
        var listener = new TcpListener(IPAddress.Any, port) { ExclusiveAddressUse = true };
        try
        {
            listener.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        finally
        {
            listener.Stop();
        }
    }
}
