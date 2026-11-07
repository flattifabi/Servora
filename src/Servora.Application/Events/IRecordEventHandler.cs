namespace Servora.Application.Events;

/// <summary>
/// Handles committed record changes asynchronously (outside the saving transaction). Register implementations as
/// scoped services: <c>services.AddScoped&lt;IRecordEventHandler, MyHandler&gt;()</c>. Each handler runs in its own
/// DI scope with a system actor; failures are logged and isolated from other handlers.
/// </summary>
public interface IRecordEventHandler
{
    /// <summary>Gets the execution order. Lower values run first.</summary>
    int Order => 100;

    /// <summary>Handles the event.</summary>
    /// <param name="recordEvent">The event.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when handling is done.</returns>
    Task HandleAsync(RecordChangedEvent recordEvent, CancellationToken cancellationToken);
}
