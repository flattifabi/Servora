namespace Servora.Installer.Abstractions;

/// <summary>
/// Registers the Windows Event Log source of the host. The service account cannot create the source itself, so the
/// elevated installer does it. Faked in unit tests.
/// </summary>
internal interface IEventLogRegistrar
{
    /// <summary>Creates or updates the event source in the Application log.</summary>
    /// <param name="source">The source name.</param>
    /// <param name="installDirectory">The install directory (used to locate the message DLL).</param>
    void Register(string source, string installDirectory);

    /// <summary>Removes the event source. Does nothing when it does not exist.</summary>
    /// <param name="source">The source name.</param>
    void Unregister(string source);
}
