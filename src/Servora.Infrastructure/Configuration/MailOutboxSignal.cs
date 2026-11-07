namespace Servora.Infrastructure.Configuration;

/// <summary>Wakes the mail outbox processor when a message was queued, so delivery does not wait for the next poll.</summary>
public sealed class MailOutboxSignal : IDisposable
{
    private readonly SemaphoreSlim _signal = new(0, 1);

    /// <summary>Signals that work is available. Multiple signals before the processor wakes up collapse into one.</summary>
    public void Notify()
    {
        try
        {
            if (_signal.CurrentCount == 0)
            {
                _signal.Release();
            }
        }
        catch (SemaphoreFullException)
        {
            // Already signalled.
        }
    }

    /// <summary>Waits for a signal or the timeout.</summary>
    /// <param name="timeout">The maximum wait time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when signalled, <see langword="false"/> on timeout.</returns>
    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) => _signal.WaitAsync(timeout, cancellationToken);

    /// <inheritdoc />
    public void Dispose() => _signal.Dispose();
}
