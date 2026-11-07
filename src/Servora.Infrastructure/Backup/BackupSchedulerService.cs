using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Servora.Application.Common;
using Servora.Application.Observability;
using Servora.Domain.Backup;
using Servora.Domain.Observability;

namespace Servora.Infrastructure.Backup;

/// <summary>
/// Hosted service that runs scheduled backups. It re-evaluates the schedule at least once a minute (so schedule
/// changes, clock changes and daylight saving time are picked up), handles each occurrence once, catches up an
/// occurrence missed during downtime when configured, and writes queued audit events.
/// </summary>
/// <param name="scopes">The scope factory.</param>
/// <param name="settings">The settings store.</param>
/// <param name="database">The database access.</param>
/// <param name="auditFlusher">The audit flusher.</param>
/// <param name="time">The time provider.</param>
/// <param name="logger">The logger.</param>
public sealed class BackupSchedulerService(
    IServiceScopeFactory scopes,
    BackupSettingsStore settings,
    IBackupDatabase database,
    BackupAuditFlusher auditFlusher,
    TimeProvider time,
    ILogger<BackupSchedulerService> logger) : BackgroundService
{
    private static readonly TimeSpan s_maxWait = TimeSpan.FromMinutes(1);

    /// <summary>Evaluates the schedule once and runs or skips a due occurrence.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The decision.</returns>
    public async Task<BackupScheduleDecision> TickAsync(CancellationToken cancellationToken)
    {
        if (!database.IsConfigured)
        {
            return new BackupScheduleDecision(BackupScheduleAction.Idle, null, null);
        }

        var schedule = settings.GetSchedule();
        var state = settings.GetState();
        var timeZone = schedule.ResolveTimeZone(time.LocalTimeZone);
        var decision = BackupSchedulePlanner.Evaluate(schedule, timeZone, state.LastHandledOccurrence, state.ScheduleBaseline, time.GetUtcNow());

        switch (decision.Action)
        {
            case BackupScheduleAction.SkipMissed:
                settings.UpdateState(s => s.LastHandledOccurrence = decision.Occurrence);
                logger.LogInformation("Scheduled backup of {Occurrence:u} was missed while Servora was not running; catch-up is disabled.", decision.Occurrence);
                break;
            case BackupScheduleAction.RunNow:
                // Mark the occurrence first: a failing backup is reported, not retried in a tight loop.
                settings.UpdateState(s => s.LastHandledOccurrence = decision.Occurrence);
                await RunScheduledBackupAsync(decision, cancellationToken);
                break;
        }

        return decision;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), time, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = s_maxWait;
            try
            {
                if (database.IsConfigured)
                {
                    await auditFlusher.FlushAsync(stoppingToken);
                }

                var decision = await TickAsync(stoppingToken);
                if (decision.NextOccurrence is { } next)
                {
                    var untilNext = next - time.GetUtcNow();
                    delay = untilNext < s_maxWait ? (untilNext > TimeSpan.FromSeconds(1) ? untilNext : TimeSpan.FromSeconds(1)) : s_maxWait;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Backup scheduler iteration failed");
            }

            try
            {
                await Task.Delay(delay, time, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task RunScheduledBackupAsync(BackupScheduleDecision decision, CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        scope.ServiceProvider.GetRequiredService<ActorContext>().Set(ActorInfo.SystemNamed("Backup scheduler"));
        var jobs = scope.ServiceProvider.GetRequiredService<BackupJobRunner>();
        var audit = scope.ServiceProvider.GetService<IAuditService>();
        var createdBy = decision.IsCatchUp ? "Scheduler (catch-up)" : "Scheduler";
        try
        {
            var result = await jobs.RunAsync(BackupKind.Scheduled, createdBy, cancellationToken);
            if (audit is not null)
            {
                await audit.WriteAsync(
                    new AuditEntry("backup", "backup.created")
                    {
                        TargetType = "backup",
                        TargetId = result.Entry.Id,
                        TargetName = result.Entry.Id,
                        Message = decision.IsCatchUp ? "Scheduled backup (catch-up after downtime)." : "Scheduled backup.",
                    },
                    cancellationToken);
            }
        }
        catch (BackupException ex)
        {
            logger.LogError("Scheduled backup failed: {Message}", ex.Message);
            if (audit is not null)
            {
                await audit.WriteAsync(new AuditEntry("backup", "backup.failed", AuditOutcome.Failure) { TargetType = "backup", Message = $"Scheduled backup failed: {ex.Message}" }, cancellationToken);
            }
        }
    }
}
