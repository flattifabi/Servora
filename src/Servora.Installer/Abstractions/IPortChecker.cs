namespace Servora.Installer.Abstractions;

/// <summary>Checks whether a TCP port can be bound. Faked in unit tests.</summary>
internal interface IPortChecker
{
    /// <summary>Determines whether a TCP port is free on all interfaces.</summary>
    /// <param name="port">The port number.</param>
    /// <returns><see langword="true"/> when nothing listens on the port.</returns>
    bool IsAvailable(int port);
}
