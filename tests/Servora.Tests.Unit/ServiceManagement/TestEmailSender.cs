using Servora.Application.Notifications;

namespace Servora.Tests.Unit.ServiceManagement;

/// <summary>E-mail sender that records messages in memory.</summary>
public sealed class TestEmailSender : IEmailSender
{
    /// <summary>Gets the sent messages.</summary>
    public List<EmailMessage> Messages { get; } = [];

    /// <inheritdoc />
    public Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        Messages.Add(message);
        return Task.FromResult(true);
    }
}
