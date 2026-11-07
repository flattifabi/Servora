using Servora.Installer.Abstractions;

namespace Servora.Tests.Unit.Installer.Fakes;

/// <summary>Records event source registrations.</summary>
internal sealed class FakeEventLogRegistrar : IEventLogRegistrar
{
    /// <summary>Gets the registered sources (source → install directory).</summary>
    public Dictionary<string, string> Registered { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void Register(string source, string installDirectory) => Registered[source] = installDirectory;

    /// <inheritdoc />
    public void Unregister(string source) => Registered.Remove(source);
}
