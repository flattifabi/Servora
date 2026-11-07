using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Servora.Infrastructure.Backup;
using Servora.Tests.Unit.Backup.Infrastructure;
using Servora.Tests.Unit.Backup.Model;

namespace Servora.Tests.Unit.Backup;

/// <summary>Acceptance: archive tamper detection (hashes, unexpected/missing entries, signature of encrypted archives).</summary>
public sealed class BackupArchiveIntegrityTests
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task UntouchedArchive_Verifies()
    {
        using var host = await CreateHostAsync();
        var backup = await host.CreateBackupAsync();

        var result = await host.Engine.VerifyAsync(backup.Path, null, CancellationToken.None);

        Assert.True(result.IsValid, string.Join(" ", result.Problems));
        Assert.Equal(backup.Manifest!.Entries.Count, result.EntriesChecked);
    }

    [Fact]
    public async Task ModifiedTableDump_IsDetected_AndRestoreIsRefused()
    {
        using var host = await CreateHostAsync();
        var backup = await host.CreateBackupAsync();
        var entryPath = backup.Manifest!.Tables[0].Entry;
        await SqliteRoundTripTests.MutateLabAsync(host);
        var mutated = await SnapshotAsync(host);

        RewriteEntry(backup.Path, entryPath, bytes =>
        {
            bytes[^2] ^= 0x5A;
            return bytes;
        });

        var result = await host.Engine.VerifyAsync(backup.Path, null, CancellationToken.None);
        Assert.False(result.IsValid);
        Assert.Contains(result.Problems, p => p.Contains(entryPath, StringComparison.Ordinal) && p.Contains("SHA-256", StringComparison.Ordinal));

        await Assert.ThrowsAsync<BackupIntegrityException>(() => host.Engine.RestoreAsync(backup.Path, new BackupRestoreOptions(), CancellationToken.None));
        DatabaseSnapshot.AssertEqual(mutated, await SnapshotAsync(host));
    }

    [Fact]
    public async Task AddedOrRemovedEntries_AreDetected()
    {
        using var host = await CreateHostAsync();
        var backup = await host.CreateBackupAsync();
        using (var zip = ZipFile.Open(backup.Path, ZipArchiveMode.Update))
        {
            zip.GetEntry(backup.Manifest!.Tables[1].Entry)!.Delete();
            using var writer = new StreamWriter(zip.CreateEntry("attachments/evil.exe").Open());
            writer.Write("MZ");
        }

        var result = await host.Engine.VerifyAsync(backup.Path, null, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(result.Problems, p => p.Contains("missing", StringComparison.Ordinal));
        Assert.Contains(result.Problems, p => p.Contains("Unexpected entry 'attachments/evil.exe'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EncryptedArchive_WithModifiedManifest_FailsSignatureCheck()
    {
        using var host = await CreateHostAsync();
        var backup = await host.CreateBackupAsync(Password);

        RewriteEntry(backup.Path, BackupArchiveFormat.ManifestEntry, bytes =>
        {
            var manifest = JsonSerializer.Deserialize<BackupManifest>(bytes, BackupJson.Options)!;
            manifest.CreatedBy = "attacker";
            return JsonSerializer.SerializeToUtf8Bytes(manifest, BackupJson.Options);
        });

        using var reader = BackupArchiveReader.Open(backup.Path);
        var exception = Assert.Throws<BackupIntegrityException>(() => reader.Unlock(Password));
        Assert.Contains("signature", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EncryptedArchive_WithModifiedCiphertext_IsDetected()
    {
        using var host = await CreateHostAsync();
        var backup = await host.CreateBackupAsync(Password);

        RewriteEntry(backup.Path, backup.Manifest!.Tables[0].Entry, bytes =>
        {
            bytes[10] ^= 0x01;
            return bytes;
        });

        var result = await host.Engine.VerifyAsync(backup.Path, Password, CancellationToken.None);
        Assert.False(result.IsValid);
        Assert.True(result.SignatureChecked);
        await Assert.ThrowsAsync<BackupIntegrityException>(() => host.Engine.RestoreAsync(backup.Path, new BackupRestoreOptions { Password = Password }, CancellationToken.None));
    }

    [Fact]
    public async Task EncryptedArchive_HidesTableData_AndVerifiesWithoutPassword()
    {
        using var host = await CreateHostAsync();
        var backup = await host.CreateBackupAsync(Password);

        var raw = await File.ReadAllBytesAsync(backup.Path);
        Assert.DoesNotContain("Alice", Encoding.UTF8.GetString(raw), StringComparison.Ordinal);

        var withoutPassword = await host.Engine.VerifyAsync(backup.Path, null, CancellationToken.None);
        Assert.True(withoutPassword.IsValid);
        Assert.False(withoutPassword.SignatureChecked);

        var withPassword = await host.Engine.VerifyAsync(backup.Path, Password, CancellationToken.None);
        Assert.True(withPassword.IsValid);
        Assert.True(withPassword.SignatureChecked);

        var wrongPassword = await host.Engine.VerifyAsync(backup.Path, "nope nope nope", CancellationToken.None);
        Assert.False(wrongPassword.IsValid);
    }

    [Fact]
    public async Task NonArchiveFiles_AreRejected()
    {
        using var host = await CreateHostAsync();
        var path = Path.Combine(host.Paths.DataDirectory, "fake.svbak");
        await File.WriteAllTextAsync(path, "this is not a zip file");
        Assert.Throws<BackupIntegrityException>(() => BackupArchiveReader.Open(path));

        var emptyZip = Path.Combine(host.Paths.DataDirectory, "empty.svbak");
        using (ZipFile.Open(emptyZip, ZipArchiveMode.Create))
        {
        }

        var exception = Assert.Throws<BackupIntegrityException>(() => BackupArchiveReader.Open(emptyZip));
        Assert.Contains("no manifest", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TruncatedArchive_IsRejected()
    {
        using var host = await CreateHostAsync();
        var backup = await host.CreateBackupAsync();
        var bytes = await File.ReadAllBytesAsync(backup.Path);
        await File.WriteAllBytesAsync(backup.Path, bytes[..(bytes.Length / 2)]);

        var result = await host.Engine.VerifyAsync(backup.Path, null, CancellationToken.None);

        Assert.False(result.IsValid);
    }

    private static async Task<BackupTestHost> CreateHostAsync()
    {
        var host = new BackupTestHost(LabContext.Create);
        await SqliteRoundTripTests.SeedLabAsync(host);
        return host;
    }

    private static void RewriteEntry(string archive, string entryPath, Func<byte[], byte[]> change)
    {
        using var zip = ZipFile.Open(archive, ZipArchiveMode.Update);
        var entry = zip.GetEntry(entryPath)!;
        byte[] bytes;
        using (var stream = entry.Open())
        using (var buffer = new MemoryStream())
        {
            stream.CopyTo(buffer);
            bytes = buffer.ToArray();
        }

        entry.Delete();
        using var output = zip.CreateEntry(entryPath).Open();
        output.Write(change(bytes));
    }

    private static async Task<SortedDictionary<string, List<string>>> SnapshotAsync(BackupTestHost host)
    {
        await using var db = host.CreateContext();
        return await DatabaseSnapshot.CaptureAsync(db);
    }
}
