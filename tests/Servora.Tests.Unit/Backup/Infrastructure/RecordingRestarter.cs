using Servora.Application.Backup;

namespace Servora.Tests.Unit.Backup.Infrastructure;

/// <summary><see cref="IApplicationRestarter"/> that records restart requests instead of stopping the process.</summary>
internal sealed class RecordingRestarter : IApplicationRestarter
{
    /// <summary>Gets the requested restart reasons.</summary>
    public List<string> Requests { get; } = [];

    /// <inheritdoc />
    public bool RestartsAutomatically => true;

    /// <inheritdoc />
    public void RequestRestart(string reason) => Requests.Add(reason);
}
