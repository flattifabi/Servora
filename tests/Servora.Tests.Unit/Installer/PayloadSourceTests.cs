using System.IO.Compression;
using Servora.Installer.Cli;
using Servora.Installer.Core;
using Servora.Installer.Model;
using Servora.Installer.Payload;
using Servora.Installer.Platform;
using Servora.Tests.Unit.Installer.Fakes;

namespace Servora.Tests.Unit.Installer;

/// <summary>Tests for the payload sources and <see cref="PayloadStager"/> on the real file system (temporary folders).</summary>
public sealed class PayloadSourceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "servora-installer-tests-" + Guid.NewGuid().ToString("N"));

    /// <summary>Initializes a new instance of the <see cref="PayloadSourceTests"/> class.</summary>
    public PayloadSourceTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void ZipPayload_WithNestedFolder_IsStaged()
    {
        var zip = Path.Combine(_root, "Servora-1.0.0-win-x64.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            AddEntry(archive, "Servora-1.0.0/Servora.exe", "exe");
            AddEntry(archive, "Servora-1.0.0/wwwroot/app.css", "css");
        }

        var stager = new PayloadStager(new PhysicalFileSystem(), new TempEnvironment(_root));
        var staged = stager.Stage(new ZipFilePayloadSource(zip));

        Assert.True(File.Exists(Path.Combine(staged.ApplicationDirectory, "wwwroot", "app.css")));
        Assert.EndsWith("Servora-1.0.0", staged.ApplicationDirectory, StringComparison.Ordinal);

        stager.Cleanup(staged.StagingRoot);
        Assert.False(Directory.Exists(staged.StagingRoot));
    }

    [Fact]
    public void ZipPayload_WithPathTraversalEntry_IsRejected()
    {
        var zip = Path.Combine(_root, "evil.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            AddEntry(archive, "Servora.exe", "exe");
            AddEntry(archive, "../../escaped.txt", "x");
        }

        var stager = new PayloadStager(new PhysicalFileSystem(), new TempEnvironment(_root));

        Assert.Throws<IOException>(() => stager.Stage(new ZipFilePayloadSource(zip)));
        Assert.False(File.Exists(Path.Combine(_root, "escaped.txt")));
    }

    [Fact]
    public void PayloadWithoutExecutable_IsRejected()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root, "empty-app")).FullName;
        File.WriteAllText(Path.Combine(folder, "readme.txt"), "x");

        var stager = new PayloadStager(new PhysicalFileSystem(), new TempEnvironment(_root));

        var error = Assert.Throws<InstallerException>(() => stager.Stage(new DirectoryPayloadSource(folder)));
        Assert.Equal(ExitCode.PayloadMissing, error.ExitCode);
    }

    [Fact]
    public void Locator_PrefersSourceOption_ThenSideBySideAppFolder()
    {
        var environment = new FakeInstallerEnvironment { InstallerDirectory = _root };
        var locator = new PayloadLocator(environment, typeof(PayloadLocator).Assembly);

        Assert.Null(locator.Locate(new InstallerOptions()));

        var app = Directory.CreateDirectory(Path.Combine(_root, "app")).FullName;
        File.WriteAllText(Path.Combine(app, "Servora.exe"), "exe");
        Assert.IsType<DirectoryPayloadSource>(locator.Locate(new InstallerOptions()));

        var zip = Path.Combine(_root, "payload.zip");
        ZipFile.CreateFromDirectory(app, zip);
        Assert.IsType<ZipFilePayloadSource>(locator.Locate(new InstallerOptions { Source = zip }));

        Assert.Throws<InstallerException>(() => locator.Locate(new InstallerOptions { Source = Path.Combine(_root, "missing.zip") }));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Best effort cleanup of the temporary folder.
        }
    }

    private static void AddEntry(ZipArchive archive, string name, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open());
        writer.Write(content);
    }
}
