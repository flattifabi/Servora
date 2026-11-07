using System.Data;
using System.Security.Cryptography;
using Servora.Infrastructure.Backup;

namespace Servora.Tests.Unit.Backup;

/// <summary>Typed value encoding, encrypted chunk streams, manifest validation and restore ordering.</summary>
public sealed class BackupFormatTests
{
    public static TheoryData<object?> Values => new()
    {
        null,
        string.Empty,
        "Ünïcödé ✓ 'quoted' \"double\" \n newline",
        true,
        (byte)255,
        (short)-12345,
        int.MinValue,
        long.MaxValue,
        3.5f,
        double.Epsilon,
        79228162514264337593543950335m,
        -0.000000001m,
        Guid.Parse("0195f000-0000-7000-8000-000000000123"),
        new DateTime(2026, 3, 29, 1, 59, 59, 999, DateTimeKind.Utc).AddTicks(9),
        new DateTime(2026, 3, 29, 1, 59, 59, DateTimeKind.Unspecified),
        new DateTimeOffset(2026, 10, 25, 2, 30, 0, TimeSpan.FromHours(2)),
        TimeSpan.FromTicks(-123456789),
        new DateOnly(1, 1, 1),
        new TimeOnly(23, 59, 59, 999),
        new byte[] { 0, 1, 2, 3, 254, 255 },
        'x',
        (sbyte)-5,
        (ushort)65000,
        uint.MaxValue,
        ulong.MaxValue,
    };

    [Theory]
    [MemberData(nameof(Values))]
    public void ValueCodec_RoundTripsTypeAndValue(object? value)
    {
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            BackupValueCodec.Write(writer, value);
        }

        buffer.Position = 0;
        using var reader = new BinaryReader(buffer);
        var read = BackupValueCodec.Read(reader);

        Assert.Equal(value?.GetType(), read?.GetType());
        Assert.Equal(value, read);
        if (value is DateTime dateTime)
        {
            Assert.Equal(dateTime.Kind, ((DateTime)read!).Kind);
        }

        if (value is DateTimeOffset offset)
        {
            Assert.Equal(offset.Offset, ((DateTimeOffset)read!).Offset);
        }
    }

    [Fact]
    public void ValueCodec_RejectsUnsupportedTypes() =>
        Assert.Throws<NotSupportedException>(() => BackupValueCodec.Write(new BinaryWriter(new MemoryStream()), new int[] { 1 }));

    [Fact]
    public void TableDump_RoundTripsRows_AndDetectsTruncation()
    {
        using var buffer = new MemoryStream();
        using (var writer = new TableDumpWriter(buffer, 2))
        {
            writer.WriteRow(new object?[] { 1, "a" });
            writer.WriteRow(new object?[] { null, new byte[] { 7 } });
        }

        var bytes = buffer.ToArray();
        using (var reader = new TableDumpReader(new MemoryStream(bytes), 2))
        {
            var values = new object?[2];
            Assert.True(reader.ReadRow(values));
            Assert.Equal([1, "a"], values);
            Assert.True(reader.ReadRow(values));
            Assert.Null(values[0]);
            Assert.False(reader.ReadRow(values));
        }

        using var truncated = new TableDumpReader(new MemoryStream(bytes[..^3]), 2);
        var row = new object?[2];
        Assert.Throws<BackupIntegrityException>(() =>
        {
            while (truncated.ReadRow(row))
            {
            }
        });
        Assert.Throws<BackupIntegrityException>(() => new TableDumpReader(new MemoryStream(bytes), 3));
    }

    [Fact]
    public void ChunkedEncryption_RoundTrips_AndBindsEntryIdentity()
    {
        var plain = RandomNumberGenerator.GetBytes(200_000);
        using var cipher = BackupCipher.Create("a very long password", BackupCipher.MinIterations, chunkSize: 4096);
        using var encrypted = new MemoryStream();
        using (var encryptor = cipher.CreateEncryptor(encrypted, 3, "db/0001.bin"))
        {
            encryptor.Write(plain);
        }

        var cipherText = encrypted.ToArray();
        Assert.Equal(plain, Decrypt(cipher, cipherText, 3, "db/0001.bin"));

        // Moving the content to another entry (index or path) or truncating it is detected.
        Assert.Throws<BackupIntegrityException>(() => Decrypt(cipher, cipherText, 4, "db/0001.bin"));
        Assert.Throws<BackupIntegrityException>(() => Decrypt(cipher, cipherText, 3, "db/0002.bin"));
        Assert.Throws<BackupIntegrityException>(() => Decrypt(cipher, cipherText[..^100], 3, "db/0001.bin"));
        Assert.Throws<BackupIntegrityException>(() => Decrypt(cipher, cipherText[..(4096 + 21)], 3, "db/0001.bin"));
    }

    [Fact]
    public void Cipher_DetectsWrongPassword_AndOutOfRangeParameters()
    {
        using var cipher = BackupCipher.Create("first password!", BackupCipher.MinIterations);

        Assert.True(BackupCipher.IsPasswordValid("first password!", cipher.Parameters));
        Assert.False(BackupCipher.IsPasswordValid("second password", cipher.Parameters));
        Assert.Throws<BackupPasswordException>(() => BackupCipher.Open("second password", cipher.Parameters));

        var hostile = new BackupEncryptionManifest { Iterations = int.MaxValue, Salt = cipher.Parameters.Salt, KeyCheck = cipher.Parameters.KeyCheck, ChunkSize = 4096 };
        Assert.Throws<BackupIntegrityException>(() => BackupCipher.Open("first password!", hostile));
    }

    [Theory]
    [InlineData("keys/../../evil.xml", false)]
    [InlineData("/etc/passwd", false)]
    [InlineData("attachments\\x", false)]
    [InlineData("C:/x", false)]
    [InlineData("data/branding/./logo.svg", false)]
    [InlineData("attachments/ab12", true)]
    [InlineData("data/branding/sub dir/logo.svg", true)]
    public void EntryPathValidation(string path, bool expected) => Assert.Equal(expected, BackupManifestValidator.IsSafeEntryPath(path));

    [Theory]
    [InlineData("TEXT", true)]
    [InlineData("nvarchar(max)", true)]
    [InlineData("numeric(18,2)", true)]
    [InlineData("character varying(256)", true)]
    [InlineData("timestamp with time zone", true)]
    [InlineData("timestamp(3) with time zone", true)]
    [InlineData("text[]", true)]
    [InlineData("uuid); DROP TABLE users; --", false)]
    [InlineData("int'", false)]
    [InlineData("", false)]
    public void StoreTypeValidation(string storeType, bool expected) => Assert.Equal(expected, BackupManifestValidator.IsSafeStoreType(storeType));

    [Fact]
    public void ManifestValidation_RejectsHostileManifests()
    {
        var manifest = new BackupManifest
        {
            Provider = "Sqlite",
            ProviderName = "Microsoft.EntityFrameworkCore.Sqlite",
            Entries = [new BackupEntryManifest { Index = 0, Path = "keys/../x", Kind = BackupArchiveFormat.KindKey, Sha256 = new string('0', 64) }],
            Tables = [new BackupTableManifest { Name = "T", Entry = "db/missing.bin", Columns = [new BackupColumnManifest { Name = "Id", StoreType = "uuid; --" }] }],
        };

        var problems = BackupManifestValidator.Validate(manifest);

        Assert.Contains(problems, p => p.Contains("not allowed", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("missing", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("store type", StringComparison.Ordinal));
        Assert.Contains("format version", string.Join(" ", BackupManifestValidator.Validate(new BackupManifest { FormatVersion = 99, Provider = "x", ProviderName = "y" })), StringComparison.Ordinal);
    }

    [Fact]
    public void TableOrdering_PutsPrincipalsFirst_AndDefersSelfReferencesAndCycles()
    {
        var departments = Table("Departments", ("Id", false), ("ManagerId", true));
        var employees = Table("Employees", ("Id", false), ("DepartmentId", false), ("MentorId", true));
        var badges = Table("Badges", ("Id", false), ("EmployeeId", false));
        departments.ForeignKeys.Add(Fk("ManagerId", "Employees"));
        employees.ForeignKeys.Add(Fk("DepartmentId", "Departments"));
        employees.ForeignKeys.Add(Fk("MentorId", "Employees"));
        badges.ForeignKeys.Add(Fk("EmployeeId", "Employees"));

        var order = BackupTableOrdering.Compute([badges, employees, departments]);

        Assert.Equal(["Departments", "Employees", "Badges"], order.InsertOrder.Select(t => t.Name));
        Assert.Equal(["ManagerId"], order.GetDeferred(departments));
        Assert.Equal(["MentorId"], order.GetDeferred(employees));
        Assert.Empty(order.GetDeferred(badges));
    }

    [Fact]
    public void TableOrdering_RejectsCyclesOfRequiredForeignKeys()
    {
        var a = Table("A", ("Id", false), ("BId", false));
        var b = Table("B", ("Id", false), ("AId", false));
        a.ForeignKeys.Add(Fk("BId", "B"));
        b.ForeignKeys.Add(Fk("AId", "A"));

        Assert.Throws<BackupException>(() => BackupTableOrdering.Compute([a, b]));
    }

    [Fact]
    public void FileNames_AreUnique_AndRecognizeUploads()
    {
        var folder = Path.Combine(Path.GetTempPath(), "servora-backup-names-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var at = new DateTimeOffset(2026, 11, 7, 2, 0, 0, TimeSpan.Zero);
            var first = BackupFileNames.CreatePath(folder, at, Domain.Backup.BackupKind.Scheduled);
            File.WriteAllText(first, string.Empty);
            var second = BackupFileNames.CreatePath(folder, at, Domain.Backup.BackupKind.Scheduled);

            Assert.EndsWith("servora-20261107-020000-scheduled.svbak", first, StringComparison.Ordinal);
            Assert.EndsWith("servora-20261107-020000-scheduled-2.svbak", second, StringComparison.Ordinal);
            Assert.Equal(Domain.Backup.BackupKind.Uploaded, BackupFileNames.EffectiveKind("servora-20261107-020000-uploaded.svbak", Domain.Backup.BackupKind.Manual));
            Assert.True(BackupFileNames.IsValidId("servora-20261107-020000-manual.svbak"));
            Assert.False(BackupFileNames.IsValidId("../servora.svbak"));
            Assert.False(BackupFileNames.IsValidId("sub/servora.svbak"));
            Assert.False(BackupFileNames.IsValidId("servora.zip"));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static byte[] Decrypt(BackupCipher cipher, byte[] cipherText, int index, string path)
    {
        using var decryptor = cipher.CreateDecryptor(new MemoryStream(cipherText), index, path);
        using var output = new MemoryStream();
        decryptor.CopyTo(output);
        return output.ToArray();
    }

    private static BackupTableManifest Table(string name, params (string Name, bool Nullable)[] columns) => new()
    {
        Name = name,
        Entry = $"db/{name}.bin",
        PrimaryKey = ["Id"],
        Columns = columns.Select(c => new BackupColumnManifest { Name = c.Name, StoreType = "TEXT", IsNullable = c.Nullable, DbType = DbType.String }).ToList(),
    };

    private static BackupForeignKeyManifest Fk(string column, string principal) =>
        new() { Name = $"FK_{column}", Columns = [column], PrincipalTable = principal, PrincipalColumns = ["Id"] };
}
