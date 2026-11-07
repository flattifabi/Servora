using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Servora.Application.Common;
using Servora.Application.Configuration;
using Servora.Application.Notifications;
using Servora.Domain.Configuration;
using Servora.Infrastructure.Persistence;

namespace Servora.Infrastructure.Configuration;

/// <summary>
/// Background delivery of the mail outbox. Wakes up when a message is queued or every 15 seconds, claims a bounded
/// batch of due messages, sends them sequentially and schedules retries with exponential back-off
/// (<see cref="OutboxRetryPolicy"/>). Messages stuck in "sending" (process crash) are recovered after 10 minutes and
/// delivered messages are purged after 30 days. Does nothing while no database is configured or e-mail is disabled.
/// </summary>
/// <param name="scopeFactory">The scope factory.</param>
/// <param name="settingsStore">The database settings store.</param>
/// <param name="signal">The wake-up signal.</param>
/// <param name="clock">The clock.</param>
/// <param name="logger">The logger.</param>
public sealed class MailOutboxProcessor(
    IServiceScopeFactory scopeFactory,
    DatabaseSettingsStore settingsStore,
    MailOutboxSignal signal,
    IClock clock,
    ILogger<MailOutboxProcessor> logger) : BackgroundService
{
    /// <summary>The maximum number of messages claimed per batch.</summary>
    public const int BatchSize = 20;

    private static readonly TimeSpan s_pollInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan s_stuckAfter = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan s_retention = TimeSpan.FromDays(30);
    private static readonly TimeSpan s_cleanupInterval = TimeSpan.FromHours(1);
    private DateTimeOffset _lastCleanup = DateTimeOffset.MinValue;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await signal.WaitAsync(s_pollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (!settingsStore.IsConfigured)
            {
                continue;
            }

            try
            {
                int processed;
                var rounds = 0;
                do
                {
                    processed = await ProcessBatchAsync(stoppingToken);
                }
                while (processed == BatchSize && ++rounds < 10 && !stoppingToken.IsCancellationRequested);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Mail outbox processing failed.");
            }
        }
    }

    private static EmailMessage ToMessage(OutboundEmail email) =>
        new(Split(email.Recipients), email.Subject, email.HtmlBody)
        {
            Cc = Split(email.CcRecipients),
            TextBody = email.TextBody,
        };

    private static List<string> Split(string? addresses) =>
        string.IsNullOrWhiteSpace(addresses) ? [] : addresses.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ActorContext>().Set(ActorInfo.SystemNamed("Mail Outbox"));
        var services = scope.ServiceProvider;
        var dbFactory = services.GetRequiredService<IAppDbContextFactory>();
        var now = clock.UtcNow;

        await CleanupAsync(dbFactory, now, cancellationToken);

        var connection = await services.GetRequiredService<SmtpOptionsProvider>().ResolveAsync(requireEnabled: true, cancellationToken);
        if (connection is null)
        {
            return 0;
        }

        var maxAttempts = await services.GetRequiredService<ISystemSettingsService>().GetAsync(SystemSettingKeys.MaxDeliveryAttempts, 6, cancellationToken);
        var transport = services.GetRequiredService<ISmtpTransport>();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var due = await db.Set<OutboundEmail>()
            .Where(e => e.Status == OutboundEmailStatus.Pending && e.NextAttemptAt <= now)
            .OrderBy(e => e.NextAttemptAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);
        if (due.Count == 0)
        {
            return 0;
        }

        foreach (var email in due)
        {
            email.Status = OutboundEmailStatus.Sending;
            email.Attempts++;
            email.LastAttemptAt = now;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another node claimed (some of) these messages first.
            return 0;
        }

        foreach (var email in due)
        {
            var result = await transport.SendAsync(connection, ToMessage(email), cancellationToken);
            var finished = clock.UtcNow;
            if (result.Succeeded)
            {
                email.Status = OutboundEmailStatus.Sent;
                email.SentAt = finished;
                email.LastError = null;
            }
            else
            {
                var next = OutboxRetryPolicy.GetNextAttempt(email.Attempts, maxAttempts, finished);
                email.Status = next is null ? OutboundEmailStatus.Failed : OutboundEmailStatus.Pending;
                email.NextAttemptAt = next ?? finished;
                email.LastError = result.Error is { Length: > OutboundEmailLimits.MaxErrorLength } error ? error[..OutboundEmailLimits.MaxErrorLength] : result.Error;
                if (next is null)
                {
                    logger.LogWarning("E-mail {Id} failed permanently after {Attempts} attempt(s).", email.Id, email.Attempts);
                }
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return due.Count;
    }

    private async Task CleanupAsync(IAppDbContextFactory dbFactory, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var stuckBefore = now - s_stuckAfter;
        var stuck = await db.Set<OutboundEmail>()
            .Where(e => e.Status == OutboundEmailStatus.Sending && e.LastAttemptAt < stuckBefore)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);
        foreach (var email in stuck)
        {
            email.Status = OutboundEmailStatus.Pending;
            email.NextAttemptAt = now;
        }

        if (now - _lastCleanup >= s_cleanupInterval)
        {
            _lastCleanup = now;
            var purgeBefore = now - s_retention;
            var old = await db.Set<OutboundEmail>()
                .Where(e => e.Status == OutboundEmailStatus.Sent && e.SentAt < purgeBefore)
                .OrderBy(e => e.SentAt)
                .Take(500)
                .ToListAsync(cancellationToken);
            db.Set<OutboundEmail>().RemoveRange(old);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
