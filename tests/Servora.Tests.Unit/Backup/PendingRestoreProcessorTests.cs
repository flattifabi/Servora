using Microsoft.EntityFrameworkCore;
using Servora.Domain.Backup;
using Servora.Infrastructure.Backup;
using Servora.Tests.Unit.Backup.Infrastructure;
using Servora.Tests.Unit.Backup.Model;

namespace Servora.Tests.Unit.Backup;

/// <summary>Restore at startup: request handling, failure roll back and restart-loop protection.</summary>
public sealed class PendingRestoreProcessorTests
{
    [Fact]
    public async Task NoRequest_DoesNothing()
    {
        using var host = new BackupTestHost(LabContext.Create);
        Assert.False(await host.Processor.RunAsync());
        Assert.Null(host.Settings.GetState().LastRestore);
    }

    [Fact]
    public async Task FailingRestore_RollsBackToSafetyBackup_AndStartupContinues()
    {
        using var host = new BackupTestHost(LabContext.Create);
        await SqliteRoundTripTests.SeedLabAsync(host);
        var backup = await host.CreateBackupAsync();
        await SqliteRoundTripTests.MutateLabAsync(host);

        // The restore will fail while inserting the first badge of the backup (which the current data no longer has).
        await using (var db = host.CreateContext())
        {
            var code = await db.Set<LabBadge>().Select(b => b.Code).ToListAsync();
            Assert.DoesNotContain("A-1", code);
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_restore BEFORE INSERT ON \"Badges\" WHEN NEW.\"Code\" = 'A-1' BEGIN SELECT RAISE(ABORT, 'boom'); END;");
        }

        SortedDictionary<string, List<string>> current;
        await using (var db = host.CreateContext())
        {
            current = await DatabaseSnapshot.CaptureAsync(db);
        }

        var safety = await host.Jobs.RunAsync(BackupKind.PreRestore, "tests", CancellationToken.None);
        host.Requests.Write(new RestoreRequest { BackupPath = backup.Path, BackupId = backup.Id, SafetyBackupPath = safety.Entry.Path, RequestedBy = "tests" }, null, null);

        Assert.False(await host.Processor.RunAsync());

        var status = host.Settings.GetState().LastRestore!;
        Assert.False(status.Succeeded);
        Assert.Contains("boom", status.Message, StringComparison.Ordinal);
        Assert.Contains("rolled back", status.Message, StringComparison.Ordinal);
        Assert.False(host.Requests.IsPending);
        Assert.True(File.Exists(host.Settings.RestoreRequestPath + ".failed"));
        Assert.Contains(host.Settings.GetState().PendingAudits, a => a.Action == "backup.restore.failed");
        await using (var db = host.CreateContext())
        {
            DatabaseSnapshot.AssertEqual(current, await DatabaseSnapshot.CaptureAsync(db));
        }
    }

    [Fact]
    public async Task MissingArchive_FailsWithoutTouchingTheDatabase()
    {
        using var host = new BackupTestHost(LabContext.Create);
        await SqliteRoundTripTests.SeedLabAsync(host);
        host.Requests.Write(new RestoreRequest { BackupPath = Path.Combine(host.Paths.DataDirectory, "gone.svbak"), BackupId = "gone.svbak", RequestedBy = "tests" }, null, null);

        Assert.False(await host.Processor.RunAsync());
        Assert.False(host.Settings.GetState().LastRestore!.Succeeded);
    }

    [Fact]
    public void InterruptedRequests_AreRetriedOnce_ThenParked()
    {
        using var host = new BackupTestHost(LabContext.Create);
        host.Requests.Write(new RestoreRequest { BackupPath = "x.svbak", BackupId = "x.svbak", RequestedBy = "tests" }, "secret password", null);

        // Simulate crashes during processing: the ticket is taken but never completed.
        var first = host.Requests.TryTake();
        Assert.Equal("secret password", first!.Password);
        Assert.DoesNotContain("secret password", File.ReadAllText(host.Settings.RestoreRequestPath + ".processing"), StringComparison.Ordinal);
        Assert.NotNull(host.Requests.TryTake());
        Assert.Null(host.Requests.TryTake());

        Assert.False(host.Requests.IsPending);
        Assert.True(File.Exists(host.Settings.RestoreRequestPath + ".failed"));
    }
}
