using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Servora.Application.Backup;
using Servora.Application.Common;
using Servora.Application.Security;
using Servora.Domain.Backup;
using Servora.Infrastructure.Backup;
using Servora.Tests.Unit.Backup.Infrastructure;
using Servora.Tests.Unit.Backup.Model;
using Servora.Tests.Unit.Itsm;

namespace Servora.Tests.Unit.Backup;

/// <summary>Authorization, validation, auditing and the UI restore flow of <see cref="BackupService"/>.</summary>
public sealed class BackupServiceTests : IDisposable
{
    private readonly BackupTestHost _host = new(LabContext.Create);
    private readonly FakeCurrentUser _user = new();
    private readonly RecordingAuditService _audit = new();
    private readonly InMemorySecretStore _secrets = new();
    private readonly RecordingRestarter _restarter = new();
    private readonly Dictionary<string, string?> _configuration = new();

    [Fact]
    public async Task EveryOperation_RequiresItsPermission()
    {
        await SeedAsync();
        var service = CreateService();
        _user.Grant();

        Assert.Equal(ResultErrorKind.Forbidden, (await service.GetOverviewAsync()).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await service.ListAsync()).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await service.GetSettingsAsync()).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await service.GetSchemaStatusAsync()).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await service.VerifyAsync("x.svbak")).ErrorKind);

        _user.Grant(Permissions.Backup.Read);
        Assert.True((await service.ListAsync()).Succeeded);
        Assert.Equal(ResultErrorKind.Forbidden, (await service.CreateBackupAsync()).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await service.DeleteAsync("x.svbak")).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await service.OpenDownloadAsync("x.svbak")).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await service.ImportAsync(new MemoryStream(), "x.svbak")).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await service.SaveSettingsAsync(new BackupSettingsUpdate(new BackupSchedule()))).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await service.CheckRestoreAsync("x.svbak")).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await service.RequestRestoreAsync(new BackupRestoreRequestInput("x.svbak", "RESTORE"))).ErrorKind);

        _user.Grant(Permissions.Backup.Read, Permissions.Backup.Manage);
        Assert.True((await service.CreateBackupAsync()).Succeeded);
        Assert.Equal(ResultErrorKind.Forbidden, (await service.RequestRestoreAsync(new BackupRestoreRequestInput("x.svbak", "RESTORE"))).ErrorKind);
    }

    [Fact]
    public async Task ManualBackup_IsListed_Audited_AndShownInOverview()
    {
        await SeedAsync();
        var service = CreateService();

        var created = await service.CreateBackupAsync();

        Assert.True(created.Succeeded, created.Error);
        Assert.Equal(BackupKind.Manual, created.Value!.Kind);
        Assert.Equal("Test User", created.Value.CreatedBy);
        var list = (await service.ListAsync()).Value!;
        Assert.Single(list);
        var overview = (await service.GetOverviewAsync()).Value!;
        Assert.Equal(1, overview.BackupCount);
        Assert.NotNull(overview.LastSuccessfulBackupAt);
        Assert.Contains(_audit.Entries, e => e.Action == "backup.created" && e.TargetId == created.Value.Id);
    }

    [Fact]
    public async Task Restore_RequiresTypingRESTORE()
    {
        await SeedAsync();
        var service = CreateService();
        var backup = (await service.CreateBackupAsync()).Value!;

        var lowercase = await service.RequestRestoreAsync(new BackupRestoreRequestInput(backup.Id, "restore"));
        var empty = await service.RequestRestoreAsync(new BackupRestoreRequestInput(backup.Id, string.Empty));

        Assert.Equal(ResultErrorKind.Validation, lowercase.ErrorKind);
        Assert.Equal(ResultErrorKind.Validation, empty.ErrorKind);
        Assert.False(_host.Requests.IsPending);
        Assert.Empty(_restarter.Requests);
    }

    [Fact]
    public async Task RestoreFlow_CreatesSafetyBackup_Restarts_AndRestoresAtStartup()
    {
        await SeedAsync();
        var service = CreateService();
        var backup = (await service.CreateBackupAsync()).Value!;
        SortedDictionary<string, List<string>> expected;
        await using (var db = _host.CreateContext())
        {
            expected = await DatabaseSnapshot.CaptureAsync(db);
        }

        await SqliteRoundTripTests.MutateLabAsync(_host);
        var check = (await service.CheckRestoreAsync(backup.Id)).Value!;
        Assert.True(check.CanRestore, string.Join(" ", check.Problems));

        var requested = await service.RequestRestoreAsync(new BackupRestoreRequestInput(backup.Id, BackupConstants.RestoreConfirmationText));

        Assert.True(requested.Succeeded, requested.Error);
        Assert.Single(_restarter.Requests);
        Assert.True(_host.Requests.IsPending);
        Assert.Single(_host.Catalog.List(), e => e.Kind == BackupKind.PreRestore);
        Assert.Contains(_audit.Entries, e => e.Action == "backup.restore.requested");
        Assert.Equal(ResultErrorKind.Conflict, (await service.RequestRestoreAsync(new BackupRestoreRequestInput(backup.Id, "RESTORE"))).ErrorKind);

        // Next start: the restore runs before migrations.
        Assert.True(await _host.Processor.RunAsync());

        Assert.False(_host.Requests.IsPending);
        await using (var db = _host.CreateContext())
        {
            DatabaseSnapshot.AssertEqual(expected, await DatabaseSnapshot.CaptureAsync(db));
        }

        var overview = (await service.GetOverviewAsync()).Value!;
        Assert.True(overview.LastRestore!.Succeeded);
        Assert.Contains(_host.Settings.GetState().PendingAudits, a => a.Action == "backup.restored");
    }

    [Fact]
    public async Task EncryptedRestore_UsesStoredPassword_OrRefusesWrongOne()
    {
        await SeedAsync();
        var service = CreateService();
        var schedule = _host.Settings.GetSchedule();
        schedule.EncryptionEnabled = true;
        Assert.True((await service.SaveSettingsAsync(new BackupSettingsUpdate(schedule, "a strong backup password"))).Succeeded);
        _host.Passwords.Password = await _secrets.GetAsync(BackupConstants.PasswordSecretName);
        var backup = (await service.CreateBackupAsync()).Value!;
        Assert.True(backup.IsEncrypted);

        var check = (await service.CheckRestoreAsync(backup.Id)).Value!;
        Assert.True(check.RequiresPassword);
        Assert.True(check.StoredPasswordWorks);

        var wrong = await service.RequestRestoreAsync(new BackupRestoreRequestInput(backup.Id, "RESTORE", "not the password"));
        Assert.Equal(ResultErrorKind.Validation, wrong.ErrorKind);

        var ok = await service.RequestRestoreAsync(new BackupRestoreRequestInput(backup.Id, "RESTORE"));
        Assert.True(ok.Succeeded, ok.Error);
        Assert.True(await _host.Processor.RunAsync());
    }

    [Fact]
    public async Task SaveSettings_ValidatesEncryptionPassword_AndNeverAuditsIt()
    {
        var service = CreateService();
        var schedule = new BackupSchedule { Enabled = true, EncryptionEnabled = true };

        Assert.Equal(ResultErrorKind.Validation, (await service.SaveSettingsAsync(new BackupSettingsUpdate(schedule))).ErrorKind);
        Assert.Equal(ResultErrorKind.Validation, (await service.SaveSettingsAsync(new BackupSettingsUpdate(schedule, "short"))).ErrorKind);
        Assert.Equal(ResultErrorKind.Validation, (await service.SaveSettingsAsync(new BackupSettingsUpdate(new BackupSchedule { Weekdays = [] }))).ErrorKind);

        var saved = await service.SaveSettingsAsync(new BackupSettingsUpdate(schedule, "a strong backup password"));

        Assert.True(saved.Succeeded, saved.Error);
        Assert.True(_host.Settings.GetSchedule().EncryptionEnabled);
        Assert.Equal("a strong backup password", await _secrets.GetAsync(BackupConstants.PasswordSecretName));
        var audit = Assert.Single(_audit.Entries, e => e.Action == "backup.schedule.updated");
        Assert.DoesNotContain("a strong backup password", JsonSerializer.Serialize(audit.Details), StringComparison.Ordinal);
        Assert.True((await service.GetSettingsAsync()).Value!.PasswordConfigured);
    }

    [Fact]
    public async Task SaveSettings_RejectsUnwritableDestination()
    {
        var service = CreateService();
        var blocker = Path.Combine(_host.Paths.DataDirectory, "a-file");
        await File.WriteAllTextAsync(blocker, "x");

        var result = await service.SaveSettingsAsync(new BackupSettingsUpdate(new BackupSchedule { DestinationFolder = Path.Combine(blocker, "sub") }));

        Assert.Equal(ResultErrorKind.Validation, result.ErrorKind);
        Assert.Contains("not writable", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Upload_AcceptsValidArchives_AsUploadedKind()
    {
        await SeedAsync();
        var service = CreateService();
        var source = await CreateForeignArchiveAsync();

        await using var stream = File.OpenRead(source);
        var result = await service.ImportAsync(stream, "from-other-server.svbak");

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(BackupKind.Uploaded, result.Value!.Kind);
        Assert.Equal(BackupKind.Manual, result.Value.OriginalKind);
        Assert.Equal(BackupIntegrity.Valid, result.Value.Integrity);
        Assert.Contains(_audit.Entries, e => e.Action == "backup.uploaded" && e.Outcome == Domain.Observability.AuditOutcome.Success);
    }

    [Fact]
    public async Task Upload_RejectsTamperedArchives_WrongExtension_AndOversizedFiles()
    {
        await SeedAsync();
        var service = CreateService();
        var source = await CreateForeignArchiveAsync();
        using (var zip = ZipFile.Open(source, ZipArchiveMode.Update))
        {
            using var writer = new StreamWriter(zip.CreateEntry("keys/injected.xml").Open());
            writer.Write("<key />");
        }

        await using (var stream = File.OpenRead(source))
        {
            var tampered = await service.ImportAsync(stream, "x.svbak");
            Assert.Equal(ResultErrorKind.Validation, tampered.ErrorKind);
        }

        Assert.Equal(ResultErrorKind.Validation, (await service.ImportAsync(new MemoryStream([1, 2, 3]), "x.zip")).ErrorKind);
        Assert.Equal(ResultErrorKind.Validation, (await service.ImportAsync(new MemoryStream([1, 2, 3]), "garbage.svbak")).ErrorKind);

        _configuration[BackupService.MaxUploadSizeKey] = "1";
        var oversized = await CreateService().ImportAsync(new MemoryStream(RandomNumberGenerator.GetBytes(1_200_000)), "big.svbak");
        Assert.Equal(ResultErrorKind.Validation, oversized.ErrorKind);

        Assert.Empty((await service.ListAsync()).Value!);
        Assert.Empty(Directory.GetFiles(_host.Settings.DefaultDestination));
    }

    [Fact]
    public async Task Delete_RemovesArchive_AndUnknownIdsAreNotFound()
    {
        await SeedAsync();
        var service = CreateService();
        var backup = (await service.CreateBackupAsync()).Value!;

        Assert.Equal(ResultErrorKind.NotFound, (await service.DeleteAsync("missing.svbak")).ErrorKind);
        Assert.Equal(ResultErrorKind.NotFound, (await service.DeleteAsync("../../etc/passwd")).ErrorKind);
        Assert.True((await service.DeleteAsync(backup.Id)).Succeeded);
        Assert.Empty((await service.ListAsync()).Value!);
        Assert.Contains(_audit.Entries, e => e.Action == "backup.deleted");
    }

    [Fact]
    public async Task Download_ReturnsTheArchive_AndIsAudited()
    {
        await SeedAsync();
        var service = CreateService();
        var backup = (await service.CreateBackupAsync()).Value!;

        var download = await service.OpenDownloadAsync(backup.Id);

        Assert.True(download.Succeeded);
        await using (download.Value!.Content)
        {
            Assert.Equal(backup.SizeBytes, download.Value.Length);
        }

        Assert.Contains(_audit.Entries, e => e.Action == "backup.downloaded");
    }

    [Fact]
    public async Task SchemaStatus_ReportsEnsureCreatedMode()
    {
        await SeedAsync();
        var status = (await CreateService().GetSchemaStatusAsync()).Value!;

        Assert.False(status.UsesMigrations);
        Assert.Empty(status.Pending);
        Assert.Null(status.Problem);
    }

    /// <inheritdoc />
    public void Dispose() => _host.Dispose();

    private async Task SeedAsync() => await SqliteRoundTripTests.SeedLabAsync(_host);

    private async Task<string> CreateForeignArchiveAsync()
    {
        var entry = await _host.CreateBackupAsync();
        var foreign = Path.Combine(_host.Paths.DataDirectory, "foreign.svbak");
        File.Move(entry.Path, foreign);
        _host.Catalog.Forget(entry.Path);
        return foreign;
    }

    private BackupService CreateService() => new(
        _user,
        _audit,
        _secrets,
        _host.Engine,
        _host.Catalog,
        _host.Settings,
        _host.Jobs,
        _host.Lock,
        _host.Requests,
        _restarter,
        _host.Database,
        new ConfigurationBuilder().AddInMemoryCollection(_configuration).Build(),
        _host.Time,
        NullLogger<BackupService>.Instance);
}
