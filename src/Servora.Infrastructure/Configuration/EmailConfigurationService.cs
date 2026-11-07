using System.Globalization;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Configuration;
using Servora.Application.Notifications;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Domain.Configuration;
using Servora.Domain.Observability;

namespace Servora.Infrastructure.Configuration;

/// <summary>Default <see cref="IEmailConfigurationService"/>.</summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="settings">The settings service.</param>
/// <param name="options">The SMTP options provider.</param>
/// <param name="secrets">The secret store.</param>
/// <param name="transport">The SMTP transport.</param>
/// <param name="branding">The branding service.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="audit">The audit service.</param>
/// <param name="clock">The clock.</param>
/// <param name="signal">The outbox wake-up signal.</param>
public sealed class EmailConfigurationService(
    IAppDbContextFactory dbFactory,
    ISystemSettingsService settings,
    SmtpOptionsProvider options,
    ISecretStore secrets,
    ISmtpTransport transport,
    IBrandingService branding,
    ICurrentUser currentUser,
    IAuditService audit,
    IClock clock,
    MailOutboxSignal signal) : IEmailConfigurationService
{
    private const int MaxBulkRetry = 500;

    /// <inheritdoc />
    public async Task<Result<SmtpSettings>> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<SmtpSettings>.Forbidden();
        }

        return Result<SmtpSettings>.Success(await options.GetSettingsAsync(cancellationToken));
    }

    /// <inheritdoc />
    public async Task<Result> SaveSettingsAsync(SmtpSettingsUpdate update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        if (!await CanManageAsync(cancellationToken))
        {
            return Result.Forbidden();
        }

        var hasPassword = await secrets.ExistsAsync(SystemSettingKeys.SmtpPasswordSecret, cancellationToken);
        var errors = SmtpSettingsValidator.Validate(update, hasPassword);
        if (errors.Count > 0)
        {
            return Result.Invalid(string.Join(" ", errors));
        }

        var result = await settings.SetManyAsync(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [SystemSettingKeys.EmailEnabled] = update.Enabled ? "true" : "false",
            [SystemSettingKeys.SmtpHost] = update.Host,
            [SystemSettingKeys.SmtpPort] = update.Port.ToString(CultureInfo.InvariantCulture),
            [SystemSettingKeys.SmtpSecurity] = update.Security.ToString(),
            [SystemSettingKeys.SmtpUserName] = update.UserName,
            [SystemSettingKeys.FromAddress] = update.FromAddress,
            [SystemSettingKeys.FromName] = update.FromName,
            [SystemSettingKeys.SmtpTimeoutSeconds] = update.TimeoutSeconds.ToString(CultureInfo.InvariantCulture),
            [SystemSettingKeys.MaxDeliveryAttempts] = update.MaxAttempts.ToString(CultureInfo.InvariantCulture),
        }, cancellationToken);
        if (!result.Succeeded)
        {
            return result;
        }

        if (update.ClearPassword && hasPassword)
        {
            await secrets.DeleteAsync(SystemSettingKeys.SmtpPasswordSecret, cancellationToken);
            await WriteSecretAuditAsync("secret.deleted", "SMTP password removed.", cancellationToken);
        }
        else if (!string.IsNullOrEmpty(update.NewPassword))
        {
            await secrets.SetAsync(SystemSettingKeys.SmtpPasswordSecret, update.NewPassword, "Password of the SMTP account used for outgoing e-mail.", cancellationToken);
            await WriteSecretAuditAsync(hasPassword ? "secret.rotated" : "secret.created", "SMTP password updated.", cancellationToken);
        }

        signal.Notify();
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> SendTestAsync(string recipient, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result.Forbidden();
        }

        recipient = recipient?.Trim() ?? string.Empty;
        if (!SmtpSettingsValidator.IsValidAddress(recipient))
        {
            return Result.Invalid("Enter a valid recipient address.");
        }

        var connection = await options.ResolveAsync(requireEnabled: false, cancellationToken);
        if (connection is null)
        {
            return Result.Invalid("Save the SMTP server and sender address before sending a test e-mail.");
        }

        var brand = await branding.GetAsync(cancellationToken);
        var message = BuildTestMessage(recipient, brand.ProductName, connection, currentUser.DisplayName, clock.UtcNow);
        var result = await transport.SendAsync(connection, message, cancellationToken);

        var now = clock.UtcNow;
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            db.Set<OutboundEmail>().Add(new OutboundEmail
            {
                Recipients = recipient,
                Subject = message.Subject,
                HtmlBody = message.HtmlBody,
                TextBody = message.TextBody,
                Status = result.Succeeded ? OutboundEmailStatus.Sent : OutboundEmailStatus.Failed,
                Attempts = 1,
                NextAttemptAt = now,
                LastAttemptAt = now,
                SentAt = result.Succeeded ? now : null,
                LastError = result.Succeeded ? null : Truncate(result.Error, OutboundEmailLimits.MaxErrorLength),
                Source = "test",
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        await audit.WriteAsync(new AuditEntry(SystemSettingsService.AuditCategory, "email.test", result.Succeeded ? AuditOutcome.Success : AuditOutcome.Failure)
        {
            TargetType = "email",
            TargetName = recipient,
            Message = result.Succeeded ? $"Test e-mail sent via {connection.Host}:{connection.Port}." : $"Test e-mail via {connection.Host}:{connection.Port} failed.",
        }, cancellationToken);
        return result;
    }

    /// <inheritdoc />
    public async Task<Result<OutboxStatistics>> GetOutboxStatisticsAsync(CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<OutboxStatistics>.Forbidden();
        }

        var since = clock.UtcNow.AddHours(-24);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var set = db.Set<OutboundEmail>().AsNoTracking();
        var statistics = new OutboxStatistics(
            await set.CountAsync(e => e.Status == OutboundEmailStatus.Pending || e.Status == OutboundEmailStatus.Sending, cancellationToken),
            await set.CountAsync(e => e.Status == OutboundEmailStatus.Pending && e.Attempts > 0, cancellationToken),
            await set.CountAsync(e => e.Status == OutboundEmailStatus.Sent && e.SentAt >= since, cancellationToken),
            await set.CountAsync(e => e.Status == OutboundEmailStatus.Failed, cancellationToken));
        return Result<OutboxStatistics>.Success(statistics);
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<OutboxMessageSummary>>> ListOutboxAsync(PageRequest request, OutboundEmailStatus? status = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<PagedResult<OutboxMessageSummary>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Set<OutboundEmail>().AsNoTracking();
        if (status is { } filter)
        {
            query = query.Where(e => e.Status == filter);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(e => e.Recipients.ToLower().Contains(term) || e.Subject.ToLower().Contains(term));
        }

        query = (request.SortBy, request.SortDescending) switch
        {
            ("Subject", false) => query.OrderBy(e => e.Subject),
            ("Subject", true) => query.OrderByDescending(e => e.Subject),
            ("Attempts", false) => query.OrderBy(e => e.Attempts),
            ("Attempts", true) => query.OrderByDescending(e => e.Attempts),
            ("CreatedAt", false) => query.OrderBy(e => e.CreatedAt),
            _ => query.OrderByDescending(e => e.CreatedAt),
        };

        var page = await query
            .Select(e => new OutboxMessageSummary(e.Id, e.Recipients, e.Subject, e.Status, e.Attempts, e.CreatedAt, e.NextAttemptAt, e.SentAt, e.LastError, e.Source))
            .ToPagedResultAsync(request, cancellationToken);
        return Result<PagedResult<OutboxMessageSummary>>.Success(page);
    }

    /// <inheritdoc />
    public async Task<Result<OutboxMessageDetail>> GetOutboxMessageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<OutboxMessageDetail>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var e = await db.Set<OutboundEmail>().AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (e is null)
        {
            return Result<OutboxMessageDetail>.NotFound("The message was not found.");
        }

        var summary = new OutboxMessageSummary(e.Id, e.Recipients, e.Subject, e.Status, e.Attempts, e.CreatedAt, e.NextAttemptAt, e.SentAt, e.LastError, e.Source);
        return Result<OutboxMessageDetail>.Success(new OutboxMessageDetail(summary, e.CcRecipients, e.HtmlBody, e.TextBody));
    }

    /// <inheritdoc />
    public async Task<Result> RetryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result.Forbidden();
        }

        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var message = await db.Set<OutboundEmail>().FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
            if (message is null)
            {
                return Result.NotFound("The message was not found.");
            }

            if (message.Status is OutboundEmailStatus.Sent or OutboundEmailStatus.Sending)
            {
                return Result.Invalid("Only pending or failed messages can be retried.");
            }

            Requeue(message);
            await db.SaveChangesAsync(cancellationToken);
        }

        await audit.WriteAsync(new AuditEntry(SystemSettingsService.AuditCategory, "email.retry")
        {
            TargetType = "email",
            TargetId = id.ToString(),
            Message = "Outbox message re-queued.",
        }, cancellationToken);
        signal.Notify();
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<int>> RetryAllFailedAsync(CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result<int>.Forbidden();
        }

        int count;
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var failed = await db.Set<OutboundEmail>()
                .Where(m => m.Status == OutboundEmailStatus.Failed)
                .OrderBy(m => m.CreatedAt)
                .Take(MaxBulkRetry)
                .ToListAsync(cancellationToken);
            failed.ForEach(Requeue);
            await db.SaveChangesAsync(cancellationToken);
            count = failed.Count;
        }

        if (count > 0)
        {
            await audit.WriteAsync(new AuditEntry(SystemSettingsService.AuditCategory, "email.retry")
            {
                TargetType = "email",
                Message = $"{count} failed outbox message(s) re-queued.",
            }, cancellationToken);
            signal.Notify();
        }

        return Result<int>.Success(count);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(cancellationToken))
        {
            return Result.Forbidden();
        }

        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var message = await db.Set<OutboundEmail>().FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
            if (message is null)
            {
                return Result.NotFound("The message was not found.");
            }

            if (message.Status == OutboundEmailStatus.Sending)
            {
                return Result.Conflict("The message is being sent right now. Try again in a moment.");
            }

            db.Set<OutboundEmail>().Remove(message);
            await db.SaveChangesAsync(cancellationToken);
        }

        await audit.WriteAsync(new AuditEntry(SystemSettingsService.AuditCategory, "email.deleted")
        {
            TargetType = "email",
            TargetId = id.ToString(),
            Message = "Outbox message deleted.",
        }, cancellationToken);
        return Result.Success();
    }

    /// <summary>Builds the test message. HTML-encodes every dynamic value.</summary>
    /// <param name="recipient">The recipient.</param>
    /// <param name="productName">The product name.</param>
    /// <param name="connection">The connection options (host/port/security only are used).</param>
    /// <param name="requestedBy">The requesting administrator.</param>
    /// <param name="now">The current time.</param>
    /// <returns>The message.</returns>
    internal static EmailMessage BuildTestMessage(string recipient, string productName, SmtpConnectionOptions connection, string requestedBy, DateTimeOffset now)
    {
        var product = WebUtility.HtmlEncode(productName);
        var server = WebUtility.HtmlEncode($"{connection.Host}:{connection.Port}");
        var security = connection.Security == SmtpSecurityMode.StartTls ? "STARTTLS" : "none";
        var user = WebUtility.HtmlEncode(requestedBy);
        var timestamp = now.ToString("u", CultureInfo.InvariantCulture);
        var html = $"""
            <div style="font-family:Segoe UI,Arial,sans-serif;max-width:560px;margin:0 auto;padding:24px;color:#0f1424">
              <div style="font-size:20px;font-weight:700;margin-bottom:12px">{product} e-mail delivery works</div>
              <p style="color:#334058;line-height:1.55">This is a test message sent from the system settings of {product}. Outgoing e-mail is configured correctly.</p>
              <table style="margin-top:16px;font-size:13px;color:#5d6880">
                <tr><td style="padding:2px 12px 2px 0">SMTP server</td><td>{server}</td></tr>
                <tr><td style="padding:2px 12px 2px 0">Security</td><td>{security}</td></tr>
                <tr><td style="padding:2px 12px 2px 0">Requested by</td><td>{user}</td></tr>
                <tr><td style="padding:2px 12px 2px 0">Time (UTC)</td><td>{timestamp}</td></tr>
              </table>
            </div>
            """;
        var text = $"{productName} e-mail delivery works.\n\nSMTP server: {connection.Host}:{connection.Port}\nSecurity: {security}\nRequested by: {requestedBy}\nTime (UTC): {timestamp}\n";
        return new EmailMessage([recipient], $"{productName}: test e-mail", html) { TextBody = text };
    }

    private static void Requeue(OutboundEmail message)
    {
        message.Status = OutboundEmailStatus.Pending;
        message.Attempts = 0;
        message.NextAttemptAt = DateTimeOffset.UtcNow;
        message.LastError = null;
    }

    private static string? Truncate(string? value, int max) => value is null || value.Length <= max ? value : value[..max];

    private Task<bool> CanManageAsync(CancellationToken cancellationToken) =>
        currentUser.HasPermissionAsync(Permissions.Configuration.ManageSettings, cancellationToken);

    private Task WriteSecretAuditAsync(string action, string message, CancellationToken cancellationToken) =>
        audit.WriteAsync(new AuditEntry(SystemSettingsService.AuditCategory, action)
        {
            TargetType = "secret",
            TargetId = SystemSettingKeys.SmtpPasswordSecret,
            TargetName = SystemSettingKeys.SmtpPasswordSecret,
            Message = message,
        }, cancellationToken);
}
