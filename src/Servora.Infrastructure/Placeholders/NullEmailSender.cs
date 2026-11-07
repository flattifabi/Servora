using Microsoft.Extensions.Logging;
using Servora.Application.Notifications;

namespace Servora.Infrastructure.Placeholders;

/// <summary>Placeholder <see cref="IEmailSender"/> that only logs, until the Configuration module (T-03) registers SMTP delivery.</summary>
/// <remarks>Placeholder registered by the core kernel so every module can run in isolation. The owning module registers the real implementation later in the DI container, which takes precedence.</remarks>
/// <param name="logger">The logger.</param>
public sealed class NullEmailSender(ILogger<NullEmailSender> logger) : IEmailSender
{
    /// <inheritdoc />
    public Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("E-mail delivery is not configured; message '{Subject}' to {Count} recipient(s) was not sent.", message.Subject, message.To.Count);
        return Task.FromResult(false);
    }
}
