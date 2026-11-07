using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Servora.Application.Common;
using Servora.Application.Configuration;
using Servora.Domain.Configuration;

namespace Servora.Tests.Unit.Configuration;

/// <summary>Tests for <see cref="SystemSettingsService"/> against an in-memory SQLite database.</summary>
public sealed class SystemSettingsServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();
    private readonly RecordingAuditService _audit = new();
    private readonly TestCurrentUser _user = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    [Fact]
    public async Task Getters_ReturnDefaults_WhenNothingIsStored()
    {
        var service = CreateService();

        Assert.Equal("Servora", await service.GetStringAsync(SystemSettingKeys.ProductName));
        Assert.Equal(587, await service.GetAsync(SystemSettingKeys.SmtpPort, 0));
        Assert.False(await service.GetAsync(SystemSettingKeys.EmailEnabled, true));
        Assert.Equal(SmtpSecurityMode.StartTls, await service.GetAsync(SystemSettingKeys.SmtpSecurity, SmtpSecurityMode.None));
        Assert.Equal(string.Empty, await service.GetStringAsync("unknown.key"));
    }

    [Fact]
    public async Task SetAsync_PersistsNormalizedValue_InvalidatesCache_AndAudits()
    {
        var service = CreateService();
        Assert.Equal("#7c6cff", await service.GetStringAsync(SystemSettingKeys.AccentColor));

        var result = await service.SetAsync(SystemSettingKeys.AccentColor, "#ABC");

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("#aabbcc", await service.GetStringAsync(SystemSettingKeys.AccentColor));
        var entry = Assert.Single(_audit.Entries);
        Assert.Equal("setting.updated", entry.Action);
        Assert.Equal(SystemSettingKeys.AccentColor, entry.TargetId);

        await using var db = await _database.CreateDbContextAsync();
        var row = await db.Set<SystemSetting>().SingleAsync();
        Assert.Equal(SettingCategories.Branding, row.Category);
        Assert.Equal("color", row.ValueType);
    }

    [Fact]
    public async Task SetManyAsync_IsAllOrNothing()
    {
        var service = CreateService();

        var result = await service.SetManyAsync(new Dictionary<string, string?>
        {
            [SystemSettingKeys.ProductName] = "Contoso Hub",
            [SystemSettingKeys.SmtpPort] = "70000",
        });

        Assert.Equal(ResultErrorKind.Validation, result.ErrorKind);
        Assert.Equal("Servora", await service.GetStringAsync(SystemSettingKeys.ProductName));
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task SetAsync_RejectsUnknownKeys_AndMissingPermission()
    {
        var service = CreateService();
        Assert.Equal(ResultErrorKind.Validation, (await service.SetAsync("does.not_exist", "x")).ErrorKind);

        _user.Granted = false;
        Assert.Equal(ResultErrorKind.Forbidden, (await service.SetAsync(SystemSettingKeys.ProductName, "x")).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await service.ListAsync()).ErrorKind);
    }

    [Fact]
    public async Task SettingTheDefault_StoresNoRow_AndUnchangedValueIsNotAudited()
    {
        var service = CreateService();

        Assert.True((await service.SetAsync(SystemSettingKeys.ProductName, "Servora")).Succeeded);
        await using (var db = await _database.CreateDbContextAsync())
        {
            Assert.False(await db.Set<SystemSetting>().AnyAsync());
        }

        await service.SetAsync(SystemSettingKeys.ProductName, "Hub");
        await service.SetAsync(SystemSettingKeys.ProductName, "Hub");
        Assert.Single(_audit.Entries);
    }

    [Fact]
    public async Task ResetAsync_RestoresTheDefault()
    {
        var service = CreateService();
        await service.SetAsync(SystemSettingKeys.DefaultPageSize, "50");
        Assert.Equal(50, await service.GetAsync(SystemSettingKeys.DefaultPageSize, 0));

        var result = await service.ResetAsync(SystemSettingKeys.DefaultPageSize);

        Assert.True(result.Succeeded);
        Assert.Equal(25, await service.GetAsync(SystemSettingKeys.DefaultPageSize, 0));
        Assert.Contains(_audit.Entries, e => e.Action == "setting.reset");
    }

    [Fact]
    public async Task ListAsync_HidesInternalSettings_AndMarksCustomizedOnes()
    {
        var service = CreateService();
        await service.SetAsync(SystemSettingKeys.CompanyName, "Contoso");

        var items = (await service.ListAsync(SettingCategories.Branding)).Value!;

        Assert.DoesNotContain(items, i => i.Key == SystemSettingKeys.LogoVersion);
        Assert.False(items.Single(i => i.Key == SystemSettingKeys.CompanyName).IsDefault);
        Assert.True(items.Single(i => i.Key == SystemSettingKeys.ProductName).IsDefault);
    }

    [Fact]
    public async Task CustomFeatureFlags_CanBeCreatedToggledAndDeleted()
    {
        var service = CreateService();
        var flags = new FeatureFlags(service);

        Assert.True((await service.CreateFeatureFlagAsync("new_dashboard", "Preview", enabled: false)).Succeeded);
        Assert.Equal(ResultErrorKind.Conflict, (await service.CreateFeatureFlagAsync("features.new_dashboard", null, true)).ErrorKind);
        Assert.False(await flags.IsEnabledAsync("new_dashboard"));

        Assert.True((await service.SetAsync("features.new_dashboard", "on")).Succeeded);
        Assert.True(await flags.IsEnabledAsync("features.new_dashboard"));
        var item = (await service.ListAsync(SettingCategories.Features)).Value!.Single(i => i.Key == "features.new_dashboard");
        Assert.True(item.IsCustom);
        Assert.Equal("New dashboard", item.Definition.Label);

        Assert.Equal(ResultErrorKind.Validation, (await service.DeleteFeatureFlagAsync(SystemSettingKeys.FeaturePortal)).ErrorKind);
        Assert.True((await service.DeleteFeatureFlagAsync("features.new_dashboard")).Succeeded);
        Assert.False(await flags.IsEnabledAsync("new_dashboard"));
    }

    [Fact]
    public async Task BuiltInFeatureFlags_HaveDefaults() =>
        Assert.True(await new FeatureFlags(CreateService()).IsEnabledAsync("self_service_portal"));

    [Fact]
    public async Task ModuleDefinitionProviders_AreMerged()
    {
        var service = new SystemSettingsService(_database, _user, _audit, _cache, [new CoreSettingDefinitionProvider(), new ExtraDefinitions()]);

        Assert.NotNull(service.FindDefinition("identity.lockout_threshold"));
        Assert.Equal(5, await service.GetAsync("identity.lockout_threshold", 0));
        Assert.True((await service.SetAsync("identity.lockout_threshold", "8")).Succeeded);
        Assert.Equal(8, await service.GetAsync("identity.lockout_threshold", 0));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cache.Dispose();
        _database.Dispose();
    }

    private SystemSettingsService CreateService() => new(_database, _user, _audit, _cache, [new CoreSettingDefinitionProvider()]);
}
