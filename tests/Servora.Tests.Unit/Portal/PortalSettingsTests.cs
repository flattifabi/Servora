using Microsoft.Extensions.DependencyInjection;
using Servora.Application.Common;
using Servora.Application.Configuration;
using Servora.Application.Observability;
using Servora.Application.Portal;
using Servora.Application.Security;
using Servora.Infrastructure.Portal;
using Servora.Tests.Unit.ServiceManagement;

namespace Servora.Tests.Unit.Portal;

/// <summary>
/// Tests of the portal settings: validation (links, lengths), authorization (<c>portal.manage</c> is enough – no
/// <c>admin.settings.manage</c> needed), audit and the hero image upload (real settings framework, in-memory SQLite).
/// </summary>
public sealed class PortalSettingsTests : IDisposable
{
    private static readonly byte[] s_jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 16, 0x4A, 0x46, 0x49, 0x46, 0, 1];

    private readonly TestDatabase _database = new();
    private readonly PortalTestPaths _paths = new();
    private readonly PortalTestUser _user = new();
    private readonly PortalTestAudit _audit = new();
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    /// <summary>Initializes a new instance of the <see cref="PortalSettingsTests"/> class.</summary>
    public PortalSettingsTests()
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddSingleton<IAppDbContextFactory>(_database);
        services.AddSingleton(_user);
        services.AddSingleton<IAuditService>(_audit);
        services.AddSingleton<IAppPaths>(_paths);
        services.AddScoped<ActorContext>();
        services.AddScoped<ICurrentUser, PortalActorAwareUser>();
        services.AddSingleton<ISettingDefinitionProvider, CoreSettingDefinitionProvider>();
        services.AddSingleton<ISettingDefinitionProvider, PortalSettingDefinitionProvider>();
        services.AddScoped<ISystemSettingsService, SystemSettingsService>();
        services.AddScoped<IPortalSettingsService, PortalSettingsService>();
        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
    }

    private IPortalSettingsService Settings => _scope.ServiceProvider.GetRequiredService<IPortalSettingsService>();

    [Theory]
    [InlineData("/portal/report", true)]
    [InlineData("/", true)]
    [InlineData("https://intranet.example.com/privacy", true)]
    [InlineData("http://intranet.example.com/a11y", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("JAVASCRIPT:alert(1)", false)]
    [InlineData("data:text/html,<script>alert(1)</script>", false)]
    [InlineData("//evil.example.com", false)]
    [InlineData("/\\evil.example.com", false)]
    [InlineData("https://user:pass@example.com", false)]
    [InlineData("ftp://example.com", false)]
    [InlineData("mailto:it@example.com", false)]
    [InlineData("/portal/report x", false)]
    [InlineData("", false)]
    public void UrlPolicy_AllowsOnlyRelativeAndHttpLinks(string url, bool allowed) =>
        Assert.Equal(allowed, PortalUrlPolicy.IsAllowed(url));

    [Fact]
    public void Validator_RejectsUnsafeLinksTooManyTermsAndMissingTitles()
    {
        Assert.Null(PortalSettingsValidator.Validate(Valid()));
        Assert.Contains("Privacy link", PortalSettingsValidator.Validate(Valid(m => m.PrivacyUrl = "javascript:alert(1)")));
        Assert.NotNull(PortalSettingsValidator.Validate(Valid(m => m.SupportUrl = "//evil.example.com")));
        Assert.NotNull(PortalSettingsValidator.Validate(Valid(m => m.HeroTitle = " ")));
        Assert.NotNull(PortalSettingsValidator.Validate(Valid(m => m.FrequentSearches = "a\nb\nc\nd\ne\nf\ng")));
        Assert.NotNull(PortalSettingsValidator.Validate(Valid(m => m.HeroTitle = new string('x', 81))));
        Assert.NotNull(PortalSettingsValidator.Validate(Valid(m => m.Currency = "BTC")));
        Assert.Null(PortalSettingsValidator.Validate(Valid(m => m.PrivacyUrl = string.Empty)));
    }

    [Fact]
    public void Model_RendersGreetingAndOnlySafeLinks()
    {
        var model = Valid(m => m.HeroGreeting = "Hello {name}, nice to have you here.");
        Assert.Equal("Hello Lea, nice to have you here.", model.GreetingFor("Lea"));
        Assert.Null(PortalSettingsModel.SafeLink("javascript:alert(1)"));
        Assert.Equal("/portal/report", PortalSettingsModel.SafeLink(" /portal/report "));
        Assert.Equal(["Laptop", "VPN"], Valid(m => m.FrequentSearches = "Laptop\n\nlaptop\nVPN").FrequentSearchTerms);
    }

    [Fact]
    public async Task Defaults_AreReturnedForPortalUsers()
    {
        _user.Become(Guid.NewGuid(), "Lea Mueller", Permissions.Portal.Access);
        var settings = (await Settings.GetAsync()).Value!;
        Assert.Equal("Good work starts here.", settings.HeroTitle);
        Assert.Equal(["Laptop", "Microsoft 365", "VPN access"], settings.FrequentSearchTerms);
        Assert.Equal("EUR", settings.Currency);
        Assert.Null(settings.HeroImageUrl);
    }

    [Fact]
    public async Task Everything_IsForbiddenWithoutPortalPermissions()
    {
        _user.Become(Guid.NewGuid(), "Nobody");
        Assert.Equal(ResultErrorKind.Forbidden, (await Settings.GetAsync()).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await Settings.OpenHeroImageAsync()).ErrorKind);

        _user.Become(Guid.NewGuid(), "Lea Mueller", Permissions.Portal.Access, Permissions.Configuration.ManageSettings);
        Assert.Equal(ResultErrorKind.Forbidden, (await Settings.SaveAsync(Valid())).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await Settings.UploadHeroImageAsync(new MemoryStream(s_jpeg))).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await Settings.RemoveHeroImageAsync()).ErrorKind);
    }

    [Fact]
    public async Task Save_WithPortalManageOnly_StoresTheSettings_AndAudits()
    {
        _user.Become(Guid.NewGuid(), "Paula Portal", Permissions.Portal.Manage);
        var model = Valid(m =>
        {
            m.HeroTitle = "Welcome to IT.";
            m.PrivacyUrl = "https://intranet.example.com/privacy";
            m.Currency = "CHF";
        });
        var result = await Settings.SaveAsync(model);
        Assert.True(result.Succeeded, result.Error);

        var stored = (await Settings.GetAsync()).Value!;
        Assert.Equal("Welcome to IT.", stored.HeroTitle);
        Assert.Equal("https://intranet.example.com/privacy", stored.PrivacyUrl);
        Assert.Equal("CHF", stored.Currency);
        Assert.Contains(_audit.Entries, e => e.Action == "portal.settings.updated");
        Assert.Contains(_audit.Entries, e => e.Action == "setting.updated" && e.TargetId == PortalSettingKeys.HeroTitle);

        Assert.Equal(ResultErrorKind.Validation, (await Settings.SaveAsync(Valid(m => m.ContactUrl = "javascript:alert(1)"))).ErrorKind);
        Assert.Equal("Welcome to IT.", (await Settings.GetAsync()).Value!.HeroTitle);
    }

    [Fact]
    public async Task HeroImage_IsTypeCheckedStoredAndRemoved()
    {
        _user.Become(Guid.NewGuid(), "Paula Portal", Permissions.Portal.Manage);
        Assert.Equal(ResultErrorKind.Validation, (await Settings.UploadHeroImageAsync(new MemoryStream("<svg/>"u8.ToArray()))).ErrorKind);
        Assert.Equal(ResultErrorKind.Validation, (await Settings.UploadHeroImageAsync(new MemoryStream(new byte[IPortalSettingsService.MaxHeroImageBytes + 1]))).ErrorKind);

        Assert.True((await Settings.UploadHeroImageAsync(new MemoryStream(s_jpeg))).Succeeded);
        var url = (await Settings.GetAsync()).Value!.HeroImageUrl;
        Assert.StartsWith(IPortalSettingsService.HeroImagePath + "?v=", url, StringComparison.Ordinal);

        _user.Become(Guid.NewGuid(), "Lea Mueller", Permissions.Portal.Access);
        var image = await Settings.OpenHeroImageAsync();
        Assert.True(image.Succeeded);
        Assert.Equal("image/jpeg", image.Value!.ContentType);
        await image.Value.Content.DisposeAsync();

        _user.Become(Guid.NewGuid(), "Paula Portal", Permissions.Portal.Manage);
        Assert.True((await Settings.RemoveHeroImageAsync()).Succeeded);
        Assert.Null((await Settings.GetAsync()).Value!.HeroImageUrl);
        Assert.Equal(ResultErrorKind.NotFound, (await Settings.OpenHeroImageAsync()).ErrorKind);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
        _database.Dispose();
        _paths.Dispose();
    }

    private static PortalSettingsModel Valid(Action<PortalSettingsModel>? change = null)
    {
        var model = new PortalSettingsModel
        {
            HeroGreeting = "Hello {name}",
            HeroTitle = "Good work starts here.",
            HeroSubtitle = "Everything in one place.",
            SearchPlaceholder = "Search",
            FrequentSearches = "Laptop\nVPN",
            SupportTitle = "Questions?",
            SupportUrl = "/portal/report",
            ContactUrl = "/portal/report",
            Currency = "EUR",
        };
        change?.Invoke(model);
        return model;
    }
}
