using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Servora.Application.Observability;
using Servora.Infrastructure.Observability.Logging;

namespace Servora.Tests.Unit.Observability;

/// <summary>Tests for the runtime debug switch.</summary>
public sealed class LogLevelSwitchTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "servora-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void DebugSwitch_ChangesLevelOfExistingLoggersWithoutRestart()
    {
        using var provider = BuildServices(new Dictionary<string, string?> { ["Logging:LogLevel:Default"] = "Information" });
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("Servora.Test.Component");
        var levels = provider.GetRequiredService<ILogLevelController>();

        Assert.False(logger.IsEnabled(LogLevel.Debug));

        levels.Apply(new LogLevelSettings { GlobalLevel = LogLevel.Debug });
        Assert.True(logger.IsEnabled(LogLevel.Debug));
        Assert.False(logger.IsEnabled(LogLevel.Trace));

        levels.Apply(LogLevelSettings.Defaults);
        Assert.False(logger.IsEnabled(LogLevel.Debug));
        Assert.True(logger.IsEnabled(LogLevel.Information));
    }

    [Fact]
    public void CategoryOverride_AppliesToPrefixOnlyAndBeatsConfiguredRules()
    {
        using var provider = BuildServices(new Dictionary<string, string?>
        {
            ["Logging:LogLevel:Default"] = "Information",
            ["Logging:LogLevel:Microsoft.EntityFrameworkCore"] = "Warning",
        });
        var factory = provider.GetRequiredService<ILoggerFactory>();
        var ef = factory.CreateLogger("Microsoft.EntityFrameworkCore.Database.Command");
        var other = factory.CreateLogger("Servora.Other");

        Assert.False(ef.IsEnabled(LogLevel.Information));

        provider.GetRequiredService<ILogLevelController>().Apply(new LogLevelSettings { Overrides = [new LogCategoryOverride("Microsoft.EntityFrameworkCore", LogLevel.Trace)] });

        Assert.True(ef.IsEnabled(LogLevel.Trace));
        Assert.False(other.IsEnabled(LogLevel.Debug));
    }

    [Fact]
    public async Task Entries_ReachRingBufferAndDisk_Redacted_WithCorrelationId()
    {
        using var provider = BuildServices(new Dictionary<string, string?> { ["Logging:LogLevel:Default"] = "Information" });
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("Servora.Test");
        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = "corr-42" }))
        {
            logger.LogWarning("Connecting with Server=db;Password={Secret}", "s3cr3t-value");
            logger.LogDebug("Hidden debug message");
        }

        var store = provider.GetRequiredService<ILogStore>();
        var entry = Assert.Single(store.Query(new LogQuery { Category = "Servora.Test" }));
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("corr-42", entry.CorrelationId);
        Assert.DoesNotContain("s3cr3t-value", entry.Message, StringComparison.Ordinal);
        Assert.Equal(SecretRedactor.Mask, entry.Properties["Secret"]);

        var sink = provider.GetRequiredService<JsonFileLogSink>();
        await sink.FlushAsync(TimeSpan.FromSeconds(5));
        var file = Assert.Single(sink.ListFiles());
        using var reader = new StreamReader(sink.OpenRead(file.Name)!);
        var content = await reader.ReadToEndAsync();
        Assert.Contains("\"correlationId\":\"corr-42\"", content, StringComparison.Ordinal);
        Assert.Contains("Password=***", content, StringComparison.Ordinal);
        Assert.DoesNotContain("s3cr3t-value", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Hidden debug message", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Compose_AutoRevertRemembersBaselineBeforeDebugSession()
    {
        var now = DateTimeOffset.UtcNow;
        var baseline = new LogLevelSettings { Overrides = [new LogCategoryOverride("Servora", LogLevel.Information)] };

        var session = LoggingAdminService.Compose(baseline, new LogLevelUpdate(LogLevel.Trace, [], 30), now);
        var extended = LoggingAdminService.Compose(session, new LogLevelUpdate(LogLevel.Debug, [], 60), now);

        Assert.Equal(now.AddMinutes(30), session.RevertAt);
        Assert.Equal(baseline.Overrides, session.RevertTo!.Overrides);
        Assert.Equal(baseline.Overrides, extended.RevertTo!.Overrides);
        Assert.Null(extended.RevertTo.GlobalLevel);
    }

    [Fact]
    public void Validate_RejectsInvalidPrefixesAndRevertTimes()
    {
        Assert.NotNull(LoggingAdminService.Validate(new LogLevelUpdate(null, [new LogCategoryOverride("bad prefix;", LogLevel.Debug)], null)));
        Assert.NotNull(LoggingAdminService.Validate(new LogLevelUpdate(LogLevel.Debug, [], 0)));
        Assert.Null(LoggingAdminService.Validate(new LogLevelUpdate(LogLevel.Debug, [new LogCategoryOverride("Servora.*", LogLevel.Trace)], 15)));
    }

    [Fact]
    public void SerializedLevels_RoundTrip()
    {
        var settings = new LogLevelSettings { GlobalLevel = LogLevel.Debug, Overrides = [new LogCategoryOverride("Servora", LogLevel.Trace)], RevertAt = DateTimeOffset.UnixEpoch };

        var copy = LoggingAdminService.DeserializeLevels(LoggingAdminService.SerializeLevels(settings));

        Assert.Equal(LogLevel.Debug, copy.GlobalLevel);
        Assert.Equal(settings.Overrides, copy.Overrides);
        Assert.Equal(DateTimeOffset.UnixEpoch, copy.RevertAt);
        Assert.Equal(LogLevelSettings.Defaults.Overrides, LoggingAdminService.DeserializeLevels("{not json").Overrides);
    }

    [Fact]
    public void Retention_DeletesExpiredFilesOnly()
    {
        using var sink = new JsonFileLogSink(_directory, new LogRetentionSettings(RetentionDays: 7));
        var old = Path.Combine(_directory, "servora-20200101.jsonl");
        var recent = Path.Combine(_directory, "servora-20990101.jsonl");
        var foreign = Path.Combine(_directory, "notes.txt");
        File.WriteAllText(old, "{}");
        File.WriteAllText(recent, "{}");
        File.WriteAllText(foreign, "keep");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-30));
        File.SetLastWriteTimeUtc(foreign, DateTime.UtcNow.AddDays(-30));

        Assert.Equal(1, sink.ApplyRetention());
        Assert.False(File.Exists(old));
        Assert.True(File.Exists(recent));
        Assert.True(File.Exists(foreign));
        Assert.Null(sink.OpenRead("../appsettings.json"));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // Best effort clean-up.
        }
    }

    private ServiceProvider BuildServices(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(builder =>
        {
            builder.AddConfiguration(configuration.GetSection("Logging"));
            builder.AddServoraLogging(_ => _directory, configuration);
        });
        return services.BuildServiceProvider();
    }
}
