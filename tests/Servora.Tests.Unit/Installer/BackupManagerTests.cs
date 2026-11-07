using Servora.Installer.Core;
using Servora.Installer.Model;
using Servora.Tests.Unit.Installer.Fakes;

namespace Servora.Tests.Unit.Installer;

/// <summary>Tests for <see cref="BackupManager"/>.</summary>
public sealed class BackupManagerTests
{
    private const string Install = @"C:\Program Files\Servora";
    private const string Root = @"C:\ProgramData\Servora\backups";

    [Fact]
    public void Create_UsesTimestampAndVersion_AndAvoidsCollisions()
    {
        var (fs, backups) = Create();
        fs.AddFile(Path.Combine(Install, "Servora.dll"), "v1");
        var none = new HashSet<string>();

        var first = backups.Create(Install, Root, "1.2.0+abc", none);
        var second = backups.Create(Install, Root, "1.2.0+abc", none);

        Assert.Equal(Path.Combine(Root, "20261107-143000-v1.2.0"), first);
        Assert.Equal(Path.Combine(Root, "20261107-143000-v1.2.0-2"), second);
        Assert.Equal("v1", fs.Content(Path.Combine(first, "Servora.dll")));
    }

    [Fact]
    public void Create_WithoutVersion_UsesTimestampOnly()
    {
        var (fs, backups) = Create();
        fs.AddFile(Path.Combine(Install, "Servora.dll"), "v1");

        Assert.Equal(Path.Combine(Root, "20261107-143000"), backups.Create(Install, Root, null, new HashSet<string>()));
    }

    [Fact]
    public void Restore_ReplacesInstallDirectoryButKeepsPreservedEntries()
    {
        var (fs, backups) = Create();
        fs.AddFile(Path.Combine(Root, "20261107-143000", "Servora.dll"), "old");
        fs.AddFile(Path.Combine(Install, "Servora.dll"), "new");
        fs.AddFile(Path.Combine(Install, "NewOnly.dll"), "new");
        fs.AddFile(Path.Combine(Install, @"App_Data\db"), "data");

        backups.Restore(Path.Combine(Root, "20261107-143000"), Install, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ProductInfo.LegacyDataFolderName });

        Assert.Equal("old", fs.Content(Path.Combine(Install, "Servora.dll")));
        Assert.False(fs.FileExists(Path.Combine(Install, "NewOnly.dll")));
        Assert.Equal("data", fs.Content(Path.Combine(Install, @"App_Data\db")));
    }

    [Fact]
    public void ListAndPrune_KeepNewestAndIgnoreForeignFolders()
    {
        var (fs, backups) = Create();
        foreach (var name in new[] { "20260101-000000-v1.0.0", "20260601-000000-v1.1.0", "20261001-000000-v1.2.0", "notes" })
        {
            fs.AddFile(Path.Combine(Root, name, "file"), name);
        }

        var listed = backups.List(Root).Select(Path.GetFileName).ToList();
        Assert.Equal(["20261001-000000-v1.2.0", "20260601-000000-v1.1.0", "20260101-000000-v1.0.0"], listed);

        var deleted = backups.Prune(Root, 1, protectedPath: Path.Combine(Root, "20260101-000000-v1.0.0"));

        Assert.Equal([Path.Combine(Root, "20260601-000000-v1.1.0")], deleted);
        Assert.True(fs.DirectoryExists(Path.Combine(Root, "20261001-000000-v1.2.0")));
        Assert.True(fs.DirectoryExists(Path.Combine(Root, "20260101-000000-v1.0.0")));
        Assert.True(fs.DirectoryExists(Path.Combine(Root, "notes")));
    }

    private static (InMemoryFileSystem FileSystem, BackupManager Backups) Create()
    {
        var fs = new InMemoryFileSystem();
        var timings = new InstallerTimings { FileRetryCount = 1, FileRetryDelay = TimeSpan.Zero };
        var backups = new BackupManager(fs, new FileTreeCopier(fs, timings), new FixedTimeProvider(new DateTimeOffset(2026, 11, 7, 14, 30, 0, TimeSpan.Zero)));
        return (fs, backups);
    }
}
