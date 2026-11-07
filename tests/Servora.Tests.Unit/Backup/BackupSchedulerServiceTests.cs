using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Servora.Application.Common;
using Servora.Application.Observability;
using Servora.Domain.Backup;
using Servora.Infrastructure.Backup;
using Servora.Tests.Unit.Backup.Infrastructure;
using Servora.Tests.Unit.Backup.Model;

namespace Servora.Tests.Unit.Backup;

/// <summary>The hosted scheduler: runs due and missed occurrences once, records state, audits and flushes queued audits.</summary>
public sealed class BackupSchedulerServiceTests
{
    [Fact]
    public async Task Tick_CatchesUpMissedRun_Once_AndAudits()
    {
        using var host = new BackupTestHost(LabContext.Create);
        await SqliteRoundTripTests.SeedLabAsync(host);
        var audit = new RecordingAuditService();
        var scheduler = CreateScheduler(host, audit);

        // Weekly on Sunday 02:00 UTC, configured on Wednesday 4 Nov; Servora was down over the weekend.
        host.Time.Now = new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
        host.Settings.SaveSchedule(new BackupSchedule { Enabled = true, Frequency = BackupFrequency.Weekly, Weekdays = [DayOfWeek.Sunday], TimeOfDay = new TimeOnly(2, 0) });
        host.Time.Now = new DateTimeOffset(2026, 11, 9, 7, 30, 0, TimeSpan.Zero);

        var first = await scheduler.TickAsync(CancellationToken.None);
        var second = await scheduler.TickAsync(CancellationToken.None);

        Assert.Equal(BackupScheduleAction.RunNow, first.Action);
        Assert.True(first.IsCatchUp);
        Assert.Equal(BackupScheduleAction.Idle, second.Action);
        Assert.Equal(new DateTimeOffset(2026, 11, 15, 2, 0, 0, TimeSpan.Zero), second.NextOccurrence);
        var scheduled = Assert.Single(host.Catalog.List());
        Assert.Equal(BackupKind.Scheduled, scheduled.Kind);
        Assert.Equal("Scheduler (catch-up)", scheduled.Manifest!.CreatedBy);
        Assert.Equal(new DateTimeOffset(2026, 11, 8, 2, 0, 0, TimeSpan.Zero), host.Settings.GetState().LastHandledOccurrence);
        Assert.Single(audit.Entries, e => e.Action == "backup.created");
    }

    [Fact]
    public async Task Tick_RecordsFailures_WithoutRetryingInALoop()
    {
        using var host = new BackupTestHost(LabContext.Create);
        await SqliteRoundTripTests.SeedLabAsync(host);
        var audit = new RecordingAuditService();
        var scheduler = CreateScheduler(host, audit);
        host.Time.Now = new DateTimeOffset(2026, 11, 7, 1, 0, 0, TimeSpan.Zero);
        host.Settings.SaveSchedule(new BackupSchedule { Enabled = true, Frequency = BackupFrequency.Daily, TimeOfDay = new TimeOnly(2, 0), EncryptionEnabled = true });
        host.Time.Now = new DateTimeOffset(2026, 11, 7, 2, 0, 30, TimeSpan.Zero);

        // Encryption is enabled but no password is configured → the run fails.
        var decision = await scheduler.TickAsync(CancellationToken.None);
        var again = await scheduler.TickAsync(CancellationToken.None);

        Assert.Equal(BackupScheduleAction.RunNow, decision.Action);
        Assert.Equal(BackupScheduleAction.Idle, again.Action);
        var state = host.Settings.GetState();
        Assert.NotNull(state.LastFailureAt);
        Assert.Contains("no backup password", state.LastFailureMessage, StringComparison.Ordinal);
        Assert.Single(audit.Entries, e => e.Action == "backup.failed");
        Assert.Empty(host.Catalog.List());
    }

    [Fact]
    public async Task AuditFlusher_WritesQueuedEvents_AndClearsThem()
    {
        using var host = new BackupTestHost(LabContext.Create);
        var audit = new RecordingAuditService();
        host.Settings.UpdateState(s => s.PendingAudits.Add(new PendingBackupAudit { Action = "backup.restored", BackupId = "b.svbak", Message = "ok", Actor = "Jane", At = host.Time.GetUtcNow() }));
        var flusher = new BackupAuditFlusher(BuildScopes(host, audit), host.Settings);

        Assert.Equal(1, await flusher.FlushAsync());

        var entry = Assert.Single(audit.Entries);
        Assert.Equal("backup.restored", entry.Action);
        Assert.Equal("Jane", entry.ActorNameOverride);
        Assert.Empty(host.Settings.GetState().PendingAudits);
    }

    private static BackupSchedulerService CreateScheduler(BackupTestHost host, RecordingAuditService audit)
    {
        var scopes = BuildScopes(host, audit);
        return new BackupSchedulerService(scopes, host.Settings, host.Database, new BackupAuditFlusher(scopes, host.Settings), host.Time, NullLogger<BackupSchedulerService>.Instance);
    }

    private static IServiceScopeFactory BuildScopes(BackupTestHost host, RecordingAuditService audit)
    {
        var services = new ServiceCollection();
        services.AddScoped<ActorContext>();
        services.AddSingleton(host.Jobs);
        services.AddSingleton<IAuditService>(audit);
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }
}
