using Servora.Application.Common;
using Servora.Application.Configuration;
using Servora.Domain.Configuration;
using Servora.Domain.Observability;

namespace Servora.Tests.Unit.Configuration;

/// <summary>Tests for <see cref="SecretAdminService"/>.</summary>
public sealed class SecretAdminServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();
    private readonly RecordingAuditService _audit = new();
    private readonly TestCurrentUser _user = new();
    private readonly InMemorySecretStore _store = new();

    [Fact]
    public async Task Create_ValidatesName_AndRejectsDuplicates()
    {
        var service = CreateService();

        Assert.Equal(ResultErrorKind.Validation, (await service.CreateAsync("Bad Name", null, "x")).ErrorKind);
        Assert.Equal(ResultErrorKind.Validation, (await service.CreateAsync("datasource.token", null, string.Empty)).ErrorKind);
        Assert.True((await service.CreateAsync("datasource.token", "CRM", "s3cret")).Succeeded);
        Assert.Equal(ResultErrorKind.Conflict, (await service.CreateAsync("datasource.token", null, "other")).ErrorKind);
        Assert.Equal("s3cret", _store.Values["datasource.token"]);
    }

    [Fact]
    public async Task Rotate_And_Delete_AreAuditedWithoutValues()
    {
        var service = CreateService();
        await service.CreateAsync("ai.openai_key", null, "first-value");

        Assert.True((await service.RotateAsync("ai.openai_key", "second-value")).Succeeded);
        Assert.Equal("second-value", _store.Values["ai.openai_key"]);
        Assert.Equal(ResultErrorKind.NotFound, (await service.RotateAsync("missing.secret", "x")).ErrorKind);
        Assert.True((await service.DeleteAsync("ai.openai_key")).Succeeded);
        Assert.Empty(_store.Values);

        Assert.Equal(["secret.created", "secret.rotated", "secret.deleted"], _audit.Entries.Select(e => e.Action));
        foreach (var entry in _audit.Entries)
        {
            Assert.DoesNotContain("value", entry.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.Null(entry.Details);
        }
    }

    [Fact]
    public async Task WithoutPermission_EverythingIsForbidden_AndDenialsAreAudited()
    {
        _user.Granted = false;
        var service = CreateService();

        Assert.Equal(ResultErrorKind.Forbidden, (await service.ListAsync(new PageRequest())).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await service.CreateAsync("email.x", null, "v")).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await service.DeleteAsync("email.x")).ErrorKind);
        Assert.All(_audit.Entries, e => Assert.Equal(AuditOutcome.Denied, e.Outcome));
        Assert.Empty(_store.Values);
    }

    [Fact]
    public async Task List_ReturnsMetadataOnly_WithSearchAndPaging()
    {
        await using (var db = await _database.CreateDbContextAsync())
        {
            db.Set<StoredSecret>().Add(new StoredSecret { Name = "email.smtp_password", ProtectedValue = "cipher-1", Description = "SMTP" });
            db.Set<StoredSecret>().Add(new StoredSecret { Name = "datasource.crm", ProtectedValue = "cipher-2", Description = "CRM token" });
            await db.SaveChangesAsync();
        }

        var service = CreateService();
        var page = (await service.ListAsync(new PageRequest { Search = "crm" })).Value!;
        var all = (await service.ListAsync(new PageRequest { PageSize = 1 })).Value!;
        var stats = (await service.GetStatisticsAsync()).Value!;

        var item = Assert.Single(page.Items);
        Assert.Equal("datasource.crm", item.Name);
        Assert.Equal(2, all.TotalCount);
        Assert.Single(all.Items);
        Assert.Equal(2, stats.Total);
        Assert.Equal(2, stats.System);

        // The read model has no property that could carry the (encrypted or plain) value.
        Assert.DoesNotContain(typeof(SecretSummary).GetProperties(), p => p.Name.Contains("Protected", StringComparison.Ordinal) || p.Name == "Value");
    }

    [Fact]
    public void ProtectedValue_IsMarkedNotExposed() =>
        Assert.NotNull(typeof(StoredSecret).GetProperty(nameof(StoredSecret.ProtectedValue))!.GetCustomAttributes(typeof(Servora.Domain.Common.NotExposedAttribute), true).SingleOrDefault());

    /// <inheritdoc />
    public void Dispose() => _database.Dispose();

    private SecretAdminService CreateService() => new(_database, _store, _user, _audit, new FixedClock());
}
