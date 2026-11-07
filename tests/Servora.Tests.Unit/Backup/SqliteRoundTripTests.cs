using Microsoft.EntityFrameworkCore;
using Servora.Domain.Activity;
using Servora.Domain.Backup;
using Servora.Domain.Configuration;
using Servora.Domain.Identity;
using Servora.Domain.Itsm;
using Servora.Infrastructure.Backup;
using Servora.Infrastructure.Persistence;
using Servora.Tests.Unit.Backup.Infrastructure;
using Servora.Tests.Unit.Backup.Model;

namespace Servora.Tests.Unit.Backup;

/// <summary>Acceptance: SQLite round trip (seed → backup → change → restore → equal), including cycles, self references and files.</summary>
public sealed class SqliteRoundTripTests
{
    [Fact]
    public async Task Restore_ReturnsExactRows_IncludingForeignKeyCycleAndNullableSelfReferences()
    {
        using var host = new BackupTestHost(LabContext.Create);
        await SeedLabAsync(host);
        var before = await SnapshotAsync(host);

        var backup = await host.CreateBackupAsync();
        Assert.Equal(3, backup.Manifest!.Tables.Count);
        Assert.Null(backup.Manifest.Migration);

        await MutateLabAsync(host);
        Assert.NotEqual(before["Employees"], (await SnapshotAsync(host))["Employees"]);

        var result = await host.Engine.RestoreAsync(backup.Path, new BackupRestoreOptions(), CancellationToken.None);

        Assert.Equal(backup.Manifest.TotalRows, result.Rows);
        DatabaseSnapshot.AssertEqual(before, await SnapshotAsync(host));
    }

    [Fact]
    public async Task Restore_KeepsAutoIncrementWorking_ForNewRows()
    {
        using var host = new BackupTestHost(LabContext.Create);
        await SeedLabAsync(host);
        var backup = await host.CreateBackupAsync();
        await host.Engine.RestoreAsync(backup.Path, new BackupRestoreOptions(), CancellationToken.None);

        await using var db = (LabContext)host.CreateContext();
        var employee = await db.Set<LabEmployee>().FirstAsync();
        var maxId = await db.Set<LabBadge>().MaxAsync(b => b.Id);
        var badge = new LabBadge { EmployeeId = employee.Id, Code = "NEW", IssuedAt = DateTimeOffset.UtcNow };
        db.Add(badge);
        await db.SaveChangesAsync();
        Assert.True(badge.Id > maxId);
    }

    [Fact]
    public async Task EncryptedBackup_RoundTrips_WithPassword()
    {
        using var host = new BackupTestHost(LabContext.Create);
        await SeedLabAsync(host);
        var before = await SnapshotAsync(host);

        var backup = await host.CreateBackupAsync(password: "correct horse battery staple");
        Assert.NotNull(backup.Manifest!.Encryption);

        await MutateLabAsync(host);
        await host.Engine.RestoreAsync(backup.Path, new BackupRestoreOptions { Password = "correct horse battery staple" }, CancellationToken.None);

        DatabaseSnapshot.AssertEqual(before, await SnapshotAsync(host));
    }

    [Fact]
    public async Task EncryptedBackup_IsRefused_WithWrongOrMissingPassword_AndDatabaseStaysUntouched()
    {
        using var host = new BackupTestHost(LabContext.Create);
        await SeedLabAsync(host);
        var backup = await host.CreateBackupAsync(password: "correct horse battery staple");
        await MutateLabAsync(host);
        var mutated = await SnapshotAsync(host);

        await Assert.ThrowsAsync<BackupPasswordException>(() =>
            host.Engine.RestoreAsync(backup.Path, new BackupRestoreOptions { Password = "wrong password 123" }, CancellationToken.None));
        await Assert.ThrowsAsync<BackupPasswordException>(() =>
            host.Engine.RestoreAsync(backup.Path, new BackupRestoreOptions(), CancellationToken.None));

        DatabaseSnapshot.AssertEqual(mutated, await SnapshotAsync(host));
    }

    [Fact]
    public async Task ServoraModel_RoundTrips_WithKeysAndAttachments()
    {
        using var host = new BackupTestHost(cs => new ServoraDbContext(new DbContextOptionsBuilder<ServoraDbContext>().UseSqlite(cs).Options), "servora.db");
        var keyFile = Path.Combine(host.Paths.KeyDirectory, "key-11111111-1111-1111-1111-111111111111.xml");
        await File.WriteAllTextAsync(keyFile, "<key id=\"1\" />");
        var attachmentFile = Path.Combine(host.Paths.AttachmentDirectory, "a1b2c3");
        await File.WriteAllBytesAsync(attachmentFile, [1, 2, 3, 4, 5]);
        var brandingFile = Path.Combine(host.Paths.DataDirectory, "branding", "logo.svg");
        Directory.CreateDirectory(Path.GetDirectoryName(brandingFile)!);
        await File.WriteAllTextAsync(brandingFile, "<svg />");

        await using (var db = (ServoraDbContext)host.CreateContext())
        {
            await db.Database.EnsureCreatedAsync();
            var role = new Role { Name = "itil", Label = "ITIL" };
            var manager = new User { UserName = "boss", NormalizedUserName = "BOSS", DisplayName = "Boss", LastSignInAt = DateTimeOffset.UtcNow };
            var user = new User { UserName = "jane", NormalizedUserName = "JANE", DisplayName = "Jane", ManagerId = manager.Id, PasswordHash = "hash" };
            var parentGroup = new Group { Name = "IT" };
            var group = new Group { Name = "Service Desk", ParentId = parentGroup.Id, ManagerId = manager.Id };
            var parentIncident = new Incident { Number = "INC0001001", ShortDescription = "Mail down", OpenedAt = DateTimeOffset.UtcNow, AssignedToId = user.Id };
            var childIncident = new Incident { Number = "INC0001002", ShortDescription = "Cannot send", OpenedAt = DateTimeOffset.UtcNow, ParentIncidentId = parentIncident.Id };
            db.AddRange(role, manager, user, parentGroup, group, parentIncident, childIncident);
            db.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
            db.Add(new GroupMember { GroupId = group.Id, UserId = user.Id });
            db.Add(new StoredSecret { Name = "smtp.password", ProtectedValue = "CfDJ8-protected" });
            db.Add(new Attachment { TableName = "incident", RecordId = parentIncident.Id, FileName = "log.txt", StorageKey = "a1b2c3", SizeBytes = 5, UploadedBy = "Jane", UploadedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        var before = await SnapshotAsync(host);
        var backup = await host.CreateBackupAsync();
        Assert.Contains(backup.Manifest!.Entries, e => e.Kind == BackupArchiveFormat.KindKey);
        Assert.Contains(backup.Manifest.Entries, e => e.Path == "attachments/a1b2c3");
        Assert.Contains(backup.Manifest.Entries, e => e.Path == "data/branding/logo.svg");

        await using (var db = (ServoraDbContext)host.CreateContext())
        {
            db.RemoveRange(await db.Set<Incident>().ToListAsync());
            (await db.Set<User>().SingleAsync(u => u.UserName == "jane")).DisplayName = "Changed";
            await db.SaveChangesAsync();
        }

        File.Delete(attachmentFile);
        File.Delete(brandingFile);
        var newerKey = Path.Combine(host.Paths.KeyDirectory, "key-22222222-2222-2222-2222-222222222222.xml");
        await File.WriteAllTextAsync(newerKey, "<key id=\"2\" />");
        await File.WriteAllTextAsync(keyFile, "<key id=\"1\" rotated=\"true\" />");

        await host.Engine.RestoreAsync(backup.Path, new BackupRestoreOptions(), CancellationToken.None);

        DatabaseSnapshot.AssertEqual(before, await SnapshotAsync(host));
        Assert.Equal([1, 2, 3, 4, 5], await File.ReadAllBytesAsync(attachmentFile));
        Assert.True(File.Exists(brandingFile));
        Assert.True(File.Exists(newerKey), "Newer keys are kept (the key ring is merged, never replaced).");
        Assert.Contains("rotated", await File.ReadAllTextAsync(keyFile));
    }

    [Fact]
    public async Task Backup_CanExcludeAttachments()
    {
        using var host = new BackupTestHost(LabContext.Create);
        await SeedLabAsync(host);
        await File.WriteAllBytesAsync(Path.Combine(host.Paths.AttachmentDirectory, "file1"), [9]);

        var entry = await host.Engine.CreateAsync(
            new BackupCreateOptions(BackupKind.Manual, "tests", host.Settings.DefaultDestination) { IncludeAttachments = false },
            CancellationToken.None);

        Assert.False(entry.Manifest!.IncludesAttachments);
        Assert.DoesNotContain(entry.Manifest.Entries, e => e.Kind == BackupArchiveFormat.KindAttachment);
    }

    /// <summary>Seeds the lab model: two departments with managers (cycle), employees with mentors (self references) and badges.</summary>
    /// <param name="host">The host.</param>
    /// <returns>A task.</returns>
    internal static async Task SeedLabAsync(BackupTestHost host)
    {
        await using var db = (LabContext)host.CreateContext();
        await db.Database.EnsureCreatedAsync();
        var sales = new LabDepartment { Name = "Sales" };
        var support = new LabDepartment { Name = "Support – Ünïcödé ✓" };
        db.AddRange(sales, support);
        await db.SaveChangesAsync();

        var alice = new LabEmployee { Name = "Alice", DepartmentId = sales.Id };
        var bob = new LabEmployee { Name = "Bob", DepartmentId = sales.Id, MentorId = alice.Id };
        var carol = new LabEmployee { Name = "Carol", DepartmentId = support.Id, MentorId = bob.Id };
        db.AddRange(alice, bob, carol);
        await db.SaveChangesAsync();

        alice.MentorId = carol.Id;
        sales.ManagerId = alice.Id;
        support.ManagerId = carol.Id;
        db.AddRange(
            new LabBadge
            {
                EmployeeId = alice.Id, Code = "A-1", Photo = [0, 1, 2, 255], IssuedAt = new DateTimeOffset(2026, 3, 29, 1, 30, 0, TimeSpan.FromHours(2)),
                Score = 1234.5678m, Active = true, ValidUntil = new DateOnly(2027, 12, 31), Ratio = 0.1 + 0.2, Shift = TimeSpan.FromHours(8.5),
            },
            new LabBadge { EmployeeId = bob.Id, Code = "B-1", IssuedAt = DateTimeOffset.UnixEpoch, Score = -0.0001m, Ratio = double.MaxValue },
            new LabBadge { EmployeeId = carol.Id, Code = "C-1 'quoted' \"double\"", IssuedAt = DateTimeOffset.MaxValue, Score = 0m });
        await db.SaveChangesAsync();
    }

    /// <summary>Changes the lab data so a restore is observable.</summary>
    /// <param name="host">The host.</param>
    /// <returns>A task.</returns>
    internal static async Task MutateLabAsync(BackupTestHost host)
    {
        await using var db = (LabContext)host.CreateContext();
        var sales = await db.Set<LabDepartment>().SingleAsync(d => d.Name == "Sales");
        sales.Name = "Sales (renamed)";
        var bob = await db.Set<LabEmployee>().SingleAsync(e => e.Name == "Bob");
        bob.MentorId = null;
        db.Remove(await db.Set<LabBadge>().FirstAsync());
        db.Add(new LabEmployee { Name = "Dave", DepartmentId = sales.Id, MentorId = bob.Id });
        await db.SaveChangesAsync();
    }

    private static async Task<SortedDictionary<string, List<string>>> SnapshotAsync(BackupTestHost host)
    {
        await using var db = host.CreateContext();
        return await DatabaseSnapshot.CaptureAsync(db);
    }
}
