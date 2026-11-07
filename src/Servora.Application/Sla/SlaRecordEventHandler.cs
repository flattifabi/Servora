using Servora.Application.Events;

namespace Servora.Application.Sla;

/// <summary>Feeds committed record changes into the <see cref="SlaEngine"/>.</summary>
/// <param name="engine">The SLA engine.</param>
public sealed class SlaRecordEventHandler(SlaEngine engine) : IRecordEventHandler
{
    /// <inheritdoc />
    public int Order => 200;

    /// <inheritdoc />
    public Task HandleAsync(RecordChangedEvent recordEvent, CancellationToken cancellationToken) =>
        engine.ProcessEventAsync(recordEvent, cancellationToken);
}
