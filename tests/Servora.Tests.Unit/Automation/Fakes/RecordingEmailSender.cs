using System.Collections.Concurrent;
using Servora.Application.Notifications;

namespace Servora.Tests.Unit.Automation.Fakes;

/// <summary><see cref="IEmailSender"/> that records messages instead of sending them.</summary>
internal sealed class RecordingEmailSender : IEmailSender
{
    /// <summary>Gets the sent messages.</summary>
    public ConcurrentQueue<EmailMessage> Sent { get; } = new();

    /// <inheritdoc />
    public Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        Sent.Enqueue(message);
        return Task.FromResult(true);
    }
}
