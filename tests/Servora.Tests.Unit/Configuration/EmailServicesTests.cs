using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Servora.Application.Common;
using Servora.Application.Configuration;
using Servora.Application.Notifications;
using Servora.Domain.Configuration;
using Servora.Infrastructure.Configuration;
using Servora.Infrastructure.Persistence;

namespace Servora.Tests.Unit.Configuration;

/// <summary>Tests for the queued e-mail sender, SMTP settings administration, test e-mails and branding.</summary>
public sealed class EmailServicesTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();
    private readonly RecordingAuditService _audit = new();
    private readonly TestCurrentUser _user = new();
    private readonly InMemorySecretStore _secrets = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly RecordingSmtpTransport _transport = new();
    private readonly TestAppPaths _paths = new();
    private readonly MailOutboxSignal _signal = new();
    private readonly SystemSettingsService _settings;

    /// <summary>Initializes a new instance of the <see cref="EmailServicesTests"/> class.</summary>
    public EmailServicesTests()
    {
        _settings = new SystemSettingsService(_database, _user, _audit, _cache, [new CoreSettingDefinitionProvider()]);
    }

    [Fact]
    public async Task QueuedSender_ReturnsFalse_WhenDeliveryIsDisabled()
    {
        var sender = CreateSender();

        Assert.False(await sender.SendAsync(new EmailMessage(["a@example.com"], "Hi", "<p>Hi</p>")));
        await using var db = await _database.CreateDbContextAsync();
        Assert.False(await db.Set<OutboundEmail>().AnyAsync());
    }

    [Fact]
    public async Task QueuedSender_QueuesCleanedMessage()
    {
        await _settings.SetAsync(SystemSettingKeys.EmailEnabled, "true");
        var sender = CreateSender();

        var queued = await sender.SendAsync(new EmailMessage(["a@example.com", "not an address", "A@example.com", "b@example.com\r\nBcc: x@evil.test"], "Line1\r\nBcc: evil@example.com", "<p>Hi</p>")
        {
            Cc = ["c@example.com", "a@example.com"],
        });

        Assert.True(queued);
        await using var db = await _database.CreateDbContextAsync();
        var email = await db.Set<OutboundEmail>().SingleAsync();
        Assert.Equal("a@example.com", email.Recipients);
        Assert.Equal("c@example.com", email.CcRecipients);
        Assert.DoesNotContain('\n', email.Subject);
        Assert.Equal(OutboundEmailStatus.Pending, email.Status);
        Assert.Equal("notification", email.Source);
    }

    [Fact]
    public async Task QueuedSender_RejectsMessagesWithoutValidRecipients()
    {
        await _settings.SetAsync(SystemSettingKeys.EmailEnabled, "true");

        Assert.False(await CreateSender().SendAsync(new EmailMessage(["nobody"], "Hi", "<p>Hi</p>")));
    }

    [Fact]
    public async Task SaveSettings_StoresPasswordInSecretStore_NeverInSettings()
    {
        var service = CreateEmailService();
        var update = new SmtpSettingsUpdate
        {
            Enabled = true,
            Host = "smtp.example.com",
            Port = 587,
            UserName = "servora",
            NewPassword = "Top-Secret-1",
            FromAddress = "servora@example.com",
            FromName = "Servora",
        };

        var result = await service.SaveSettingsAsync(update);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("Top-Secret-1", _secrets.Values[SystemSettingKeys.SmtpPasswordSecret]);
        var settings = (await service.GetSettingsAsync()).Value!;
        Assert.True(settings.HasPassword);
        Assert.True(settings.Enabled);
        await using (var db = await _database.CreateDbContextAsync())
        {
            Assert.DoesNotContain(await db.Set<SystemSetting>().Select(s => s.Value).ToListAsync(), v => v != null && v.Contains("Top-Secret-1", StringComparison.Ordinal));
        }

        Assert.DoesNotContain(_audit.Entries, e => (e.Message ?? string.Empty).Contains("Top-Secret-1", StringComparison.Ordinal) || (e.Details?.ToString() ?? string.Empty).Contains("Top-Secret-1", StringComparison.Ordinal));
        Assert.Contains(_audit.Entries, e => e.Action == "secret.created");

        // Saving again without a new password keeps the stored one; clearing removes it.
        update.NewPassword = null;
        Assert.True((await service.SaveSettingsAsync(update)).Succeeded);
        Assert.True(_secrets.Values.ContainsKey(SystemSettingKeys.SmtpPasswordSecret));
        update.ClearPassword = true;
        update.UserName = string.Empty;
        Assert.True((await service.SaveSettingsAsync(update)).Succeeded);
        Assert.False(_secrets.Values.ContainsKey(SystemSettingKeys.SmtpPasswordSecret));
    }

    [Fact]
    public async Task SaveSettings_RejectsInvalidConfiguration()
    {
        var result = await CreateEmailService().SaveSettingsAsync(new SmtpSettingsUpdate { Enabled = true, Host = string.Empty, Port = 465 });

        Assert.Equal(ResultErrorKind.Validation, result.ErrorKind);
    }

    [Fact]
    public async Task SendTest_UsesSavedSettings_AndRecordsOutboxEntry()
    {
        var service = CreateEmailService();
        Assert.Equal(ResultErrorKind.Validation, (await service.SendTestAsync("admin@example.com")).ErrorKind);

        await service.SaveSettingsAsync(new SmtpSettingsUpdate { Enabled = false, Host = "smtp.example.com", Port = 587, UserName = "u", NewPassword = "pw", FromAddress = "servora@example.com", FromName = "Servora" });
        var ok = await service.SendTestAsync("admin@example.com");

        Assert.True(ok.Succeeded);
        var (options, message) = Assert.Single(_transport.Sent);
        Assert.Equal("pw", options.Password);
        Assert.DoesNotContain("pw", options.ToString(), StringComparison.Ordinal);
        Assert.Equal(["admin@example.com"], message.To);

        _transport.NextResult = Result.Failure("SMTP 535: authentication failed");
        var failed = await service.SendTestAsync("admin@example.com");

        Assert.False(failed.Succeeded);
        var statistics = (await service.GetOutboxStatisticsAsync()).Value!;
        Assert.Equal(1, statistics.SentLast24Hours);
        Assert.Equal(1, statistics.Failed);
        Assert.Equal(1, (await service.RetryAllFailedAsync()).Value);
        Assert.Equal(1, (await service.GetOutboxStatisticsAsync()).Value!.Pending);
    }

    [Fact]
    public void TestMessage_EncodesDynamicValues()
    {
        var options = new SmtpConnectionOptions { Host = "smtp.example.com", Port = 587, Security = SmtpSecurityMode.StartTls, FromAddress = "a@example.com" };

        var message = EmailConfigurationService.BuildTestMessage("a@example.com", "<script>x</script>", options, "Eve <b>", DateTimeOffset.UnixEpoch);

        Assert.DoesNotContain("<script>", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", message.HtmlBody, StringComparison.Ordinal);
        Assert.NotNull(message.TextBody);
    }

    [Fact]
    public async Task Branding_AcceptsRasterLogos_RejectsSvgAndOversizedFiles()
    {
        var branding = CreateBranding();
        Assert.Equal(BrandingInfo.Default.ProductName, (await branding.GetAsync()).ProductName);
        Assert.False((await branding.GetAsync()).HasLogo);

        Assert.Equal(ResultErrorKind.Validation, (await branding.UploadLogoAsync(new MemoryStream("<svg onload='x'/>"u8.ToArray()))).ErrorKind);
        Assert.Equal(ResultErrorKind.Validation, (await branding.UploadLogoAsync(new MemoryStream(new byte[IBrandingService.MaxLogoBytes + 1]))).ErrorKind);

        Assert.True((await branding.UploadLogoAsync(new MemoryStream(LogoImageValidatorTests.Png))).Succeeded);
        var info = await branding.GetAsync();
        Assert.True(info.HasLogo);
        Assert.StartsWith(IBrandingService.LogoPath + "?v=", info.LogoUrl, StringComparison.Ordinal);
        var logo = await branding.OpenLogoAsync();
        Assert.NotNull(logo);
        Assert.Equal("image/png", logo.ContentType);
        await logo.Content.DisposeAsync();

        Assert.True((await branding.RemoveLogoAsync()).Succeeded);
        Assert.False((await branding.GetAsync()).HasLogo);
        Assert.Null(await branding.OpenLogoAsync());
    }

    [Fact]
    public async Task Branding_RequiresPermissionForChanges()
    {
        _user.Granted = false;
        var branding = CreateBranding();

        Assert.Equal(ResultErrorKind.Forbidden, (await branding.UploadLogoAsync(new MemoryStream(LogoImageValidatorTests.Png))).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await branding.UpdateAsync("X", null, "#000000")).ErrorKind);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _signal.Dispose();
        _cache.Dispose();
        _database.Dispose();
        _paths.Dispose();
    }

    private QueuedEmailSender CreateSender() => new(_database, _settings, new FixedClock(), _signal, NullLogger<QueuedEmailSender>.Instance);

    private BrandingService CreateBranding()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Servora:Database:Provider"] = "Sqlite",
            ["Servora:Database:ConnectionString"] = "Data Source=:memory:",
        }).Build();
        var store = new DatabaseSettingsStore(_paths, new EphemeralDataProtectionProvider(), configuration);
        return new BrandingService(_settings, store, _paths, _user, _audit, NullLogger<BrandingService>.Instance);
    }

    private EmailConfigurationService CreateEmailService() =>
        new(_database, _settings, new SmtpOptionsProvider(_settings, _secrets), _secrets, _transport, CreateBranding(), _user, _audit, new FixedClock(), _signal);
}
