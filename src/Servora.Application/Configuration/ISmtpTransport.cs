using Servora.Application.Common;
using Servora.Application.Notifications;

namespace Servora.Application.Configuration;

/// <summary>Hands a single message to an SMTP server. Implemented in the infrastructure layer.</summary>
public interface ISmtpTransport
{
    /// <summary>Sends a message. Never throws for delivery problems; returns a user-safe error without credentials instead.</summary>
    /// <param name="options">The resolved connection options.</param>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> SendAsync(SmtpConnectionOptions options, EmailMessage message, CancellationToken cancellationToken = default);
}
