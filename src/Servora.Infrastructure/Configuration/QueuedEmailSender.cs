using Microsoft.Extensions.Logging;
using Servora.Application.Common;
using Servora.Application.Configuration;
using Servora.Application.Notifications;
using Servora.Domain.Configuration;

namespace Servora.Infrastructure.Configuration;

/// <summary>
/// Real <see cref="IEmailSender"/>: validates the message and stores it in the mail outbox, from where
/// <see cref="MailOutboxProcessor"/> delivers it via SMTP with retries. Returns <see langword="false"/> when e-mail
/// delivery is disabled in the system settings.
/// </summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="settings">The settings service.</param>
/// <param name="clock">The clock.</param>
/// <param name="signal">The outbox wake-up signal.</param>
/// <param name="logger">The logger.</param>
public sealed class QueuedEmailSender(
    IAppDbContextFactory dbFactory,
    ISystemSettingsService settings,
    IClock clock,
    MailOutboxSignal signal,
    ILogger<QueuedEmailSender> logger) : IEmailSender
{
    /// <summary>The maximum number of recipients (To + Cc) per message.</summary>
    public const int MaxRecipients = 100;

    /// <inheritdoc />
    public async Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!await settings.GetAsync(SystemSettingKeys.EmailEnabled, false, cancellationToken))
        {
            logger.LogDebug("E-mail delivery is disabled; message '{Subject}' was not queued.", message.Subject);
            return false;
        }

        var to = Clean(message.To);
        var cc = Clean(message.Cc).Except(to, StringComparer.OrdinalIgnoreCase).ToList();
        if (to.Count == 0)
        {
            logger.LogWarning("E-mail '{Subject}' has no valid recipient and was not queued.", message.Subject);
            return false;
        }

        if (to.Count + cc.Count > MaxRecipients || to.Sum(a => a.Length + 1) > OutboundEmailLimits.MaxRecipientsLength || cc.Sum(a => a.Length + 1) > OutboundEmailLimits.MaxRecipientsLength)
        {
            logger.LogWarning("E-mail '{Subject}' exceeds {Max} recipients and was not queued.", message.Subject, MaxRecipients);
            return false;
        }

        if ((message.HtmlBody?.Length ?? 0) > OutboundEmailLimits.MaxBodyLength || (message.TextBody?.Length ?? 0) > OutboundEmailLimits.MaxBodyLength)
        {
            logger.LogWarning("E-mail '{Subject}' exceeds the maximum body size and was not queued.", message.Subject);
            return false;
        }

        var outbound = new OutboundEmail
        {
            Recipients = string.Join(';', to),
            CcRecipients = cc.Count == 0 ? null : string.Join(';', cc),
            Subject = Truncate((message.Subject ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim(), OutboundEmailLimits.MaxSubjectLength),
            HtmlBody = message.HtmlBody ?? string.Empty,
            TextBody = message.TextBody,
            Status = OutboundEmailStatus.Pending,
            NextAttemptAt = clock.UtcNow,
            Source = "notification",
        };

        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            db.Set<OutboundEmail>().Add(outbound);
            await db.SaveChangesAsync(cancellationToken);
        }

        signal.Notify();
        return true;
    }

    private static List<string> Clean(IEnumerable<string> addresses) =>
        addresses
            .Select(a => a?.Trim() ?? string.Empty)
            .Where(SmtpSettingsValidator.IsValidAddress)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
