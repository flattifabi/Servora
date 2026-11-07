using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Security.Authentication;
using System.Text;
using Microsoft.Extensions.Logging;
using Servora.Application.Common;
using Servora.Application.Configuration;
using Servora.Application.Notifications;

namespace Servora.Infrastructure.Configuration;

/// <summary>
/// <see cref="ISmtpTransport"/> based on <see cref="SmtpClient"/> (System.Net.Mail). Enforces the timeout through a
/// cancellation token (the client's own timeout only applies to synchronous sends) and never logs credentials.
/// </summary>
/// <param name="logger">The logger.</param>
public sealed class SmtpTransport(ILogger<SmtpTransport> logger) : ISmtpTransport
{
    /// <inheritdoc />
    public async Task<Result> SendAsync(SmtpConnectionOptions options, EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(message);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.Timeout);
        try
        {
            using var mail = BuildMessage(options, message);
            using var client = new SmtpClient(options.Host, options.Port)
            {
                DeliveryMethod = SmtpDeliveryMethod.Network,
                EnableSsl = options.Security == SmtpSecurityMode.StartTls,
                Timeout = (int)Math.Min(int.MaxValue, options.Timeout.TotalMilliseconds),
                UseDefaultCredentials = false,
                Credentials = string.IsNullOrEmpty(options.UserName) ? null : new NetworkCredential(options.UserName, options.Password ?? string.Empty),
            };

            await client.SendMailAsync(mail, timeout.Token);
            logger.LogInformation("E-mail '{Subject}' delivered to {Count} recipient(s) via {Server}.", Truncate(message.Subject, 80), message.To.Count + message.Cc.Count, options.Host);
            return Result.Success();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("SMTP delivery via {Server}:{Port} timed out after {Seconds} s.", options.Host, options.Port, options.Timeout.TotalSeconds);
            return Result.Failure($"The SMTP server did not respond within {options.Timeout.TotalSeconds:0} seconds.");
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or FormatException or AuthenticationException or IOException or System.Net.Sockets.SocketException)
        {
            // SmtpException messages contain the server reply but never the configured password.
            var reason = Describe(ex);
            logger.LogWarning("SMTP delivery via {Server}:{Port} failed: {Reason}", options.Host, options.Port, reason);
            return Result.Failure(reason);
        }
    }

    private static MailMessage BuildMessage(SmtpConnectionOptions options, EmailMessage message)
    {
        var mail = new MailMessage
        {
            From = new MailAddress(options.FromAddress, string.IsNullOrWhiteSpace(options.FromName) ? null : options.FromName, Encoding.UTF8),
            Subject = SingleLine(message.Subject),
            SubjectEncoding = Encoding.UTF8,
            HeadersEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
        };

        foreach (var to in message.To)
        {
            mail.To.Add(new MailAddress(to));
        }

        foreach (var cc in message.Cc)
        {
            mail.CC.Add(new MailAddress(cc));
        }

        if (string.IsNullOrEmpty(message.TextBody))
        {
            mail.Body = message.HtmlBody;
            mail.IsBodyHtml = true;
        }
        else
        {
            mail.Body = message.TextBody;
            mail.IsBodyHtml = false;
            mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.HtmlBody, Encoding.UTF8, MediaTypeNames.Text.Html));
        }

        return mail;
    }

    private static string Describe(Exception ex)
    {
        var parts = new List<string>();
        for (var current = ex; current is not null && parts.Count < 3; current = current.InnerException)
        {
            var text = SingleLine(current.Message);
            if (text.Length > 0 && !parts.Contains(text))
            {
                parts.Add(text);
            }
        }

        if (ex is SmtpException smtp && smtp.StatusCode != SmtpStatusCode.GeneralFailure)
        {
            parts.Insert(0, $"SMTP {(int)smtp.StatusCode} ({smtp.StatusCode}):");
        }

        return Truncate(string.Join(" ", parts), 500);
    }

    private static string SingleLine(string value) => value.Replace('\r', ' ').Replace('\n', ' ').Trim();

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max] + "…";
}
