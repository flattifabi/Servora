using Servora.Domain.Backup;
using Servora.Tests.Unit.Backup.Infrastructure;
using Servora.Tests.Unit.Backup.Model;

namespace Servora.Tests.Unit.Backup;

/// <summary>Acceptance: retention rules (keep last N scheduled, max age, keep last N safety backups, never manual/uploaded).</summary>
public sealed class BackupRetentionTests
{
    private static readonly DateTimeOffset s_now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void KeepsNewestScheduledBackups()
    {
        var items = Enumerable.Range(0, 10).Select(i => Item($"s{i}", BackupKind.Scheduled, days: i)).ToList();
        var schedule = new BackupSchedule { KeepLastScheduled = 3, MaxAgeDays = 0 };

        var delete = BackupRetentionPlanner.SelectForDeletion(items, schedule, s_now);

        Assert.Equal(["s9", "s8", "s7", "s6", "s5", "s4", "s3"], delete.Select(d => d.Id));
    }

    [Fact]
    public void DeletesScheduledBackupsOlderThanMaxAge_ButAlwaysKeepsTheNewest()
    {
        var items = new[] { Item("old1", BackupKind.Scheduled, 40), Item("old2", BackupKind.Scheduled, 35), Item("recent", BackupKind.Scheduled, 2) };
        var schedule = new BackupSchedule { KeepLastScheduled = 0, MaxAgeDays = 30 };

        Assert.Equal(["old1", "old2"], BackupRetentionPlanner.SelectForDeletion(items, schedule, s_now).Select(d => d.Id));

        var onlyOld = new[] { Item("old1", BackupKind.Scheduled, 40), Item("old2", BackupKind.Scheduled, 35) };
        Assert.Equal(["old1"], BackupRetentionPlanner.SelectForDeletion(onlyOld, schedule, s_now).Select(d => d.Id));
    }

    [Fact]
    public void KeepsLastNSafetyBackups_PerKind()
    {
        var items = Enumerable.Range(0, 4).Select(i => Item($"pm{i}", BackupKind.PreMigration, i))
            .Concat(Enumerable.Range(0, 3).Select(i => Item($"pr{i}", BackupKind.PreRestore, i)))
            .ToList();
        var schedule = new BackupSchedule { KeepLastSafety = 2 };

        var delete = BackupRetentionPlanner.SelectForDeletion(items, schedule, s_now).Select(d => d.Id).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(["pm2", "pm3", "pr2"], delete);
    }

    [Fact]
    public void NeverDeletesManualOrUploadedBackups()
    {
        var items = Enumerable.Range(0, 20).Select(i => Item($"m{i}", i % 2 == 0 ? BackupKind.Manual : BackupKind.Uploaded, 400 + i)).ToList();
        var schedule = new BackupSchedule { KeepLastScheduled = 1, MaxAgeDays = 1, KeepLastSafety = 1 };

        Assert.Empty(BackupRetentionPlanner.SelectForDeletion(items, schedule, s_now));
    }

    [Fact]
    public void ZeroMeansUnlimited()
    {
        var items = Enumerable.Range(0, 50).Select(i => Item($"s{i}", BackupKind.Scheduled, i * 10))
            .Concat(Enumerable.Range(0, 50).Select(i => Item($"p{i}", BackupKind.PreMigration, i)))
            .ToList();
        var schedule = new BackupSchedule { KeepLastScheduled = 0, MaxAgeDays = 0, KeepLastSafety = 0 };

        Assert.Empty(BackupRetentionPlanner.SelectForDeletion(items, schedule, s_now));
    }

    [Fact]
    public async Task RetentionService_DeletesArchiveFiles_AfterScheduledRuns()
    {
        using var host = new BackupTestHost(LabContext.Create);
        await SqliteRoundTripTests.SeedLabAsync(host);
        var schedule = host.Settings.GetSchedule();
        schedule.KeepLastScheduled = 2;
        schedule.MaxAgeDays = 0;
        host.Settings.SaveSchedule(schedule);
        var manual = await host.Jobs.RunAsync(BackupKind.Manual, "tests", CancellationToken.None);

        for (var i = 0; i < 4; i++)
        {
            host.Time.Advance(TimeSpan.FromDays(1));
            await host.Jobs.RunAsync(BackupKind.Scheduled, "tests", CancellationToken.None);
        }

        var remaining = host.Catalog.List();
        Assert.Equal(2, remaining.Count(e => e.Kind == BackupKind.Scheduled));
        Assert.Contains(remaining, e => e.Id == manual.Entry.Id);
        Assert.Equal(3, Directory.GetFiles(host.Settings.DefaultDestination, "*.svbak").Length);
    }

    private static BackupRetentionItem Item(string id, BackupKind kind, int days) => new(id, kind, s_now.AddDays(-days));
}
