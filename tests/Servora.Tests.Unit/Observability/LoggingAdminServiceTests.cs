using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Servora.Application.Common;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Domain.Configuration;
using Servora.Domain.Observability;
using Servora.Infrastructure.Observability;
using Servora.Infrastructure.Observability.Logging;

namespace Servora.Tests.Unit.Observability;

/// <summary>Tests for the debug switch use cases: live level change, persistence, audit, auto-revert and authorization.</summary>
public sealed class LoggingAdminServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "servora-tests", Guid.NewGuid().ToString("N"));
    private readonly SqliteTestDatabase _database = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly ServiceProvider _provider;

    /// <summary>Initializes a new instance of the <see cref="LoggingAdminServiceTests"/> class.</summary>
    public LoggingAdminServiceTests()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Logging:LogLevel:Default"] = "Information" }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(builder =>
        {
            builder.AddConfiguration(configuration.GetSection("Logging"));
            builder.AddServoraLogging(_ => _directory, configuration);
        });
        _provider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task DebugSwitch_ChangesLevelLive_PersistsAndAudits()
    {
        var logger = _provider.GetRequiredService<ILoggerFactory>().CreateLogger("Servora.Live");
        var service = CreateService(new TestCurrentUser(Permissions.Observability.ManageLogs));
        Assert.False(logger.IsEnabled(LogLevel.Debug));

        var result = await service.UpdateLevelsAsync(new LogLevelUpdate(LogLevel.Debug, [], null));

        Assert.True(result.Succeeded, result.Error);
        Assert.True(logger.IsEnabled(LogLevel.Debug));
        logger.LogDebug("Visible after the switch");
        Assert.Contains(_provider.GetRequiredService<ILogStore>().Query(new LogQuery { Search = "Visible after the switch" }), e => e.Level == LogLevel.Debug);

        await using var db = _database.Create();
        var persisted = await db.Set<SystemSetting>().AsNoTracking().SingleAsync(s => s.Key == ObservabilitySettingKeys.LogLevels);
        Assert.Equal(LogLevel.Debug, LoggingAdminService.DeserializeLevels(persisted.Value).GlobalLevel);
        Assert.True(await db.Set<AuditEvent>().AnyAsync(a => a.Action == "logging.levels.changed"));
    }

    [Fact]
    public async Task PersistedLevels_AreRestoredAtStartup()
    {
        await CreateService(new TestCurrentUser(Permissions.Observability.ManageLogs))
            .UpdateLevelsAsync(new LogLevelUpdate(null, [new LogCategoryOverride("Servora.Restored", LogLevel.Trace)], null));
        var controller = _provider.GetRequiredService<ILogLevelController>();
        controller.Apply(LogLevelSettings.Defaults);

        await CreateService(new TestCurrentUser()).RestorePersistedAsync();

        Assert.Single(controller.Current.Overrides);
        Assert.True(_provider.GetRequiredService<ILoggerFactory>().CreateLogger("Servora.Restored.Child").IsEnabled(LogLevel.Trace));
    }

    [Fact]
    public async Task AutoRevert_RestoresPreviousLevelsWhenDue()
    {
        var logger = _provider.GetRequiredService<ILoggerFactory>().CreateLogger("Servora.Revert");
        var service = CreateService(new TestCurrentUser(Permissions.Observability.ManageLogs));
        await service.UpdateLevelsAsync(new LogLevelUpdate(LogLevel.Trace, [], 15));
        Assert.True(logger.IsEnabled(LogLevel.Trace));

        _clock.Advance(TimeSpan.FromMinutes(10));
        Assert.False(await service.RevertIfDueAsync());
        Assert.True(logger.IsEnabled(LogLevel.Trace));

        _clock.Advance(TimeSpan.FromMinutes(6));
        Assert.True(await service.RevertIfDueAsync());
        Assert.False(logger.IsEnabled(LogLevel.Debug));
        Assert.Null(_provider.GetRequiredService<ILogLevelController>().Current.RevertAt);
    }

    [Fact]
    public async Task ChangingLevels_RequiresManagePermission_ReadingRequiresReadPermission()
    {
        var reader = CreateService(new TestCurrentUser(Permissions.Observability.ReadLogs));
        Assert.Equal(ResultErrorKind.Forbidden, (await reader.UpdateLevelsAsync(new LogLevelUpdate(LogLevel.Debug, [], null))).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await reader.ResetLevelsAsync()).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await reader.UpdateRetentionAsync(new LogRetentionSettings(7, 10))).ErrorKind);
        Assert.True((await reader.GetOverviewAsync()).Succeeded);

        var nobody = CreateService(new TestCurrentUser());
        Assert.Equal(ResultErrorKind.Forbidden, (await nobody.QueryAsync(new LogQuery())).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await nobody.ExportAsync(new LogQuery())).ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, (await nobody.OpenFileAsync("servora-20260301.jsonl")).ErrorKind);
    }

    [Fact]
    public async Task InvalidUpdate_IsRejected()
    {
        var service = CreateService(new TestCurrentUser(Permissions.Observability.ManageLogs));

        var result = await service.UpdateLevelsAsync(new LogLevelUpdate(null, [new LogCategoryOverride("bad prefix; drop", LogLevel.Debug)], null));

        Assert.Equal(ResultErrorKind.Validation, result.ErrorKind);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _provider.Dispose();
        _database.Dispose();
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // Best effort clean-up.
        }
    }

    private LoggingAdminService CreateService(TestCurrentUser user) => new(
        user,
        _database,
        _provider.GetRequiredService<ILogLevelController>(),
        _provider.GetRequiredService<ILogStore>(),
        _provider.GetRequiredService<ILogFileStore>(),
        new AuditService(_database, user, _clock, NullLogger<AuditService>.Instance),
        _clock,
        NullLogger<LoggingAdminService>.Instance);
}
