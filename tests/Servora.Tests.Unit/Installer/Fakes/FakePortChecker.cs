using Servora.Installer.Abstractions;

namespace Servora.Tests.Unit.Installer.Fakes;

/// <summary>Port checker with a configurable set of occupied ports.</summary>
internal sealed class FakePortChecker : IPortChecker
{
    /// <summary>Gets the occupied ports.</summary>
    public HashSet<int> Occupied { get; } = [];

    /// <inheritdoc />
    public bool IsAvailable(int port) => !Occupied.Contains(port);
}
