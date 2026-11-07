using Microsoft.Extensions.DependencyInjection;
using Servora.Domain.Backup;
using Servora.Infrastructure.Backup;
using Servora.Tests.Unit.Backup.Infrastructure;
using Servora.Tests.Unit.Backup.Model;

namespace Servora.Tests.Unit.Backup;

/// <summary>Disaster-recovery command line: parsing, list, create, verify and restore.</summary>
public sealed class BackupCommandLineTests
{
    [Fact]
    public void Parse_RecognizesCommandsAndOptions()
    {
        Assert.True(BackupCommandLine.IsBackupCommand(["backup", "list"]));
        Assert.False(BackupCommandLine.IsBackupCommand(["--urls", "http://*:5080"]));

        var restore = BackupCommandLine.Parse(["backup", "restore", "servora-1.svbak", "--password", "pw", "--data-dir", @"D:\Servora", "--yes", "--no-safety-backup"]);
        Assert.Empty(restore.Errors);
        Assert.Equal("restore", restore.Command);
        Assert.Equal("servora-1.svbak", restore.File);
        Assert.Equal("pw", restore.Password);
        Assert.Equal(@"D:\Servora", restore.DataDirectory);
        Assert.True(restore.AssumeYes);
        Assert.True(restore.NoSafetyBackup);

        Assert.NotEmpty(BackupCommandLine.Parse(["backup", "restore"]).Errors);
        Assert.NotEmpty(BackupCommandLine.Parse(["backup", "explode"]).Errors);
        Assert.NotEmpty(BackupCommandLine.Parse(["backup", "list", "--bogus"]).Errors);
        Assert.NotEmpty(BackupCommandLine.Parse(["backup", "create", "--password"]).Errors);
        Assert.Equal("help", BackupCommandLine.Parse(["backup"]).Command);
    }

    [Fact]
    public async Task InvalidArguments_ReturnUsageExitCode()
    {
        using var host = new BackupTestHost(LabContext.Create);
        var output = new StringWriter();

        var code = await BackupCommandLine.RunAsync(BuildServices(host), BackupCommandLine.Parse(["backup", "explode"]), TextReader.Null, output);

        Assert.Equal(BackupCommandLine.ExitUsage, code);
        Assert.Contains("Usage:", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateListVerifyRestore_WorkEndToEnd()
    {
        using var host = new BackupTestHost(LabContext.Create);
        await SqliteRoundTripTests.SeedLabAsync(host);
        var services = BuildServices(host);
        SortedDictionary<string, List<string>> expected;
        await using (var db = host.CreateContext())
        {
            expected = await DatabaseSnapshot.CaptureAsync(db);
        }

        var output = new StringWriter();
        Assert.Equal(0, await BackupCommandLine.RunAsync(services, BackupCommandLine.Parse(["backup", "create"]), TextReader.Null, output));
        var created = Assert.Single(host.Catalog.List());
        Assert.Equal(BackupKind.Manual, created.Kind);

        Assert.Equal(0, await BackupCommandLine.RunAsync(services, BackupCommandLine.Parse(["backup", "list"]), TextReader.Null, output));
        Assert.Contains(created.Id, output.ToString(), StringComparison.Ordinal);

        Assert.Equal(0, await BackupCommandLine.RunAsync(services, BackupCommandLine.Parse(["backup", "verify", created.Id]), TextReader.Null, output));
        Assert.Contains("The backup is valid.", output.ToString(), StringComparison.Ordinal);

        await SqliteRoundTripTests.MutateLabAsync(host);

        // Wrong confirmation cancels.
        Assert.Equal(1, await BackupCommandLine.RunAsync(services, BackupCommandLine.Parse(["backup", "restore", created.Id]), new StringReader("no\n"), output));

        var restoreOutput = new StringWriter();
        var code = await BackupCommandLine.RunAsync(services, BackupCommandLine.Parse(["backup", "restore", created.Path]), new StringReader("RESTORE\n"), restoreOutput);

        Assert.Equal(0, code);
        Assert.Contains("Safety backup:", restoreOutput.ToString(), StringComparison.Ordinal);
        Assert.Contains(host.Catalog.List(), e => e.Kind == BackupKind.PreRestore);
        await using (var db = host.CreateContext())
        {
            DatabaseSnapshot.AssertEqual(expected, await DatabaseSnapshot.CaptureAsync(db));
        }

        Assert.Contains(host.Settings.GetState().PendingAudits, a => a.Action == "backup.created");
        Assert.Contains(host.Settings.GetState().PendingAudits, a => a.Action == "backup.restored");
    }

    [Fact]
    public async Task Restore_OfUnknownFile_Fails()
    {
        using var host = new BackupTestHost(LabContext.Create);
        var output = new StringWriter();

        var code = await BackupCommandLine.RunAsync(BuildServices(host), BackupCommandLine.Parse(["backup", "restore", "nope.svbak", "--yes"]), TextReader.Null, output);

        Assert.Equal(BackupCommandLine.ExitFailure, code);
        Assert.Contains("was found neither", output.ToString(), StringComparison.Ordinal);
    }

    private static ServiceProvider BuildServices(BackupTestHost host)
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(host.Time);
        services.AddSingleton<IBackupDatabase>(host.Database);
        services.AddSingleton<IBackupPasswordSource>(host.Passwords);
        services.AddSingleton(host.Settings);
        services.AddSingleton(host.Catalog);
        services.AddSingleton(host.Engine);
        services.AddSingleton(host.Jobs);
        services.AddSingleton(host.Requests);
        services.AddSingleton(host.Processor);
        return services.BuildServiceProvider();
    }
}
