using System.Collections.Concurrent;
using Servora.Application.Notifications;

namespace Servora.Tests.Unit.Platform;

/// <summary>Captures sent e-mails.</summary>
public sealed class TestEmailSender : IEmailSender
{
    /// <summary>Gets the sent messages.</summary>
    public ConcurrentQueue<EmailMessage> Messages { get; } = new();

    /// <inheritdoc />
    public Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        Messages.Enqueue(message);
        return Task.FromResult(true);
    }
}
