namespace Servora.Application.Notifications;

/// <summary>Sends e-mail through the SMTP server configured in the administration UI.</summary>
public interface IEmailSender
{
    /// <summary>Sends an e-mail.</summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the message was handed to the SMTP server; <see langword="false"/> when e-mail is disabled.</returns>
    Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
