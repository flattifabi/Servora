using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Servora.Application.Common;
using Servora.Application.Configuration;
using Servora.Application.Observability;
using Servora.Infrastructure.Persistence;

namespace Servora.Infrastructure.Configuration;

/// <summary>
/// Singleton <see cref="ISetupService"/>. The schema is created with the candidate settings <em>before</em> they are
/// saved, so a failing database never leaves a half-configured instance behind. After the configuration is saved the
/// kernel <see cref="DatabaseInitializer"/> seeds the data; if that fails the same settings can be retried.
/// </summary>
/// <param name="settingsStore">The database settings store.</param>
/// <param name="providers">The provider registry.</param>
/// <param name="optionsProvider">The EF options provider.</param>
/// <param name="initializer">The database initializer.</param>
/// <param name="scopeFactory">The scope factory.</param>
/// <param name="paths">The application paths.</param>
/// <param name="tokenStore">The setup token store.</param>
/// <param name="logger">The logger.</param>
public sealed class SetupService(
    DatabaseSettingsStore settingsStore,
    DatabaseProviderRegistry providers,
    DbContextOptionsProvider optionsProvider,
    DatabaseInitializer initializer,
    IServiceScopeFactory scopeFactory,
    IAppPaths paths,
    SetupTokenStore tokenStore,
    ILogger<SetupService> logger) : ISetupService
{
    private static readonly TimeSpan s_testTimeout = TimeSpan.FromSeconds(15);
    private readonly SemaphoreSlim _installLock = new(1, 1);
    private DatabaseSettings? _incomplete;
    private bool _completed;

    /// <inheritdoc />
    public bool IsConfigured => settingsStore.IsConfigured;

    /// <inheritdoc />
    public bool HasIncompleteInstallation => Volatile.Read(ref _incomplete) is not null;

    /// <inheritdoc />
    public string DataDirectory => paths.DataDirectory;

    /// <inheritdoc />
    public string SetupTokenPath => tokenStore.FilePath;

    /// <inheritdoc />
    public IReadOnlyList<SetupProviderOption> GetProviders() =>
        providers.All.Select(p => new SetupProviderOption(p.Key, p.DisplayName, p.ExampleConnectionString)).ToList();

    /// <inheritdoc />
    public Result<string> ComposeConnectionString(SetupConnectionInput input) => ConnectionStringComposer.Compose(input, paths.DataDirectory);

    /// <inheritdoc />
    public string Redact(string provider, string connectionString)
    {
        var databaseProvider = providers.All.FirstOrDefault(p => string.Equals(p.Key, provider, StringComparison.OrdinalIgnoreCase));
        return databaseProvider?.Redact(connectionString) ?? "(unknown provider)";
    }

    /// <inheritdoc />
    public bool ValidateSetupToken(string? token) => tokenStore.Validate(token);

    /// <inheritdoc />
    public async Task<Result<TimeSpan>> TestConnectionAsync(string provider, string connectionString, CancellationToken cancellationToken = default)
    {
        var databaseProvider = providers.All.FirstOrDefault(p => string.Equals(p.Key, provider, StringComparison.OrdinalIgnoreCase));
        if (databaseProvider is null)
        {
            return Result<TimeSpan>.Invalid("Choose a supported database.");
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return Result<TimeSpan>.Invalid("Enter a connection string.");
        }

        if (databaseProvider.Key == DatabaseProviderKeys.Sqlite)
        {
            var prepared = PrepareSqliteDirectory(connectionString);
            if (prepared is not null)
            {
                return Result<TimeSpan>.Failure(prepared);
            }
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(s_testTimeout);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var error = await databaseProvider.TestConnectionAsync(connectionString, timeout.Token);
            stopwatch.Stop();
            return error is null
                ? Result<TimeSpan>.Success(stopwatch.Elapsed)
                : Result<TimeSpan>.Failure(SafeMessage(error));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result<TimeSpan>.Failure($"The connection attempt timed out after {s_testTimeout.TotalSeconds:0} seconds.");
        }
    }

    /// <inheritdoc />
    public async Task<Result> InstallAsync(string provider, string connectionString, SetupOrganizationInput organization, IProgress<SetupProgressUpdate>? progress, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(organization);
        if (!await _installLock.WaitAsync(0, cancellationToken))
        {
            return Result.Conflict("An installation is already running.");
        }

        try
        {
            if (_completed)
            {
                return Result.Conflict("Setup has already been completed.");
            }

            var databaseProvider = providers.All.FirstOrDefault(p => string.Equals(p.Key, provider, StringComparison.OrdinalIgnoreCase));
            if (databaseProvider is null)
            {
                return Result.Invalid("Choose a supported database.");
            }

            var settings = new DatabaseSettings(databaseProvider.Key, connectionString);
            if (settingsStore.IsConfigured && settingsStore.Current != _incomplete)
            {
                return Result.Conflict("A database is already configured.");
            }

            if (_incomplete is not null && _incomplete != settings)
            {
                return Result.Conflict("A previous installation attempt saved different database settings. Retry with the same settings or restart the service.");
            }

            if (organization.Validate() is { } organizationError)
            {
                return Result.Invalid(organizationError);
            }

            // 1. Connection
            Report(progress, SetupStep.ValidateConnection, SetupStepStatus.Running);
            var test = await TestConnectionAsync(settings.Provider, settings.ConnectionString, cancellationToken);
            if (!test.Succeeded)
            {
                Report(progress, SetupStep.ValidateConnection, SetupStepStatus.Failed, test.Error);
                return Result.Failure(test.Error ?? "The database is not reachable.");
            }

            Report(progress, SetupStep.ValidateConnection, SetupStepStatus.Done, $"Connected in {test.Value.TotalMilliseconds:0} ms");

            // 2. Schema (with the candidate settings, before anything is persisted)
            Report(progress, SetupStep.CreateSchema, SetupStepStatus.Running);
            try
            {
                var created = await CreateSchemaAsync(settings, cancellationToken);
                Report(progress, SetupStep.CreateSchema, SetupStepStatus.Done, created ? "Schema created" : "Existing Servora schema found");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Setup: creating the database schema failed.");
                var message = ex is InvalidOperationException ? ex.Message : "Creating the schema failed: " + SafeMessage(ex.Message);
                Report(progress, SetupStep.CreateSchema, SetupStepStatus.Failed, message);
                return Result.Failure(message);
            }

            // 3. Configuration
            Report(progress, SetupStep.SaveConfiguration, SetupStepStatus.Running);
            if (!settingsStore.IsConfigured)
            {
                await settingsStore.SaveAsync(settings, cancellationToken);
            }

            Volatile.Write(ref _incomplete, settings);
            Report(progress, SetupStep.SaveConfiguration, SetupStepStatus.Done, "Encrypted and saved to database.json");
            logger.LogInformation("Setup: database configuration saved (provider {Provider}).", settings.Provider);

            // 4. Seed data
            Report(progress, SetupStep.SeedData, SetupStepStatus.Running);
            try
            {
                await initializer.InitializeAsync(organization.IncludeSampleData, cancellationToken);
                Report(progress, SetupStep.SeedData, SetupStepStatus.Done, organization.IncludeSampleData ? "Roles, administrator and sample data created" : "Roles and administrator created");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Setup: seeding the database failed.");
                var message = "Initializing the database failed: " + SafeMessage(ex.Message) + " Fix the cause and select Install again.";
                Report(progress, SetupStep.SeedData, SetupStepStatus.Failed, message);
                return Result.Failure(message);
            }

            // 5. Organisation
            Report(progress, SetupStep.ApplyOrganization, SetupStepStatus.Running);
            var applied = await ApplyOrganizationAsync(settings, organization, cancellationToken);
            if (!applied.Succeeded)
            {
                Report(progress, SetupStep.ApplyOrganization, SetupStepStatus.Failed, applied.Error);
                return applied;
            }

            Report(progress, SetupStep.ApplyOrganization, SetupStepStatus.Done, organization.CompanyName.Length > 0 ? organization.CompanyName : organization.ProductName);

            // 6. Finish
            Report(progress, SetupStep.Finalize, SetupStepStatus.Running);
            tokenStore.Delete();
            Volatile.Write(ref _incomplete, null);
            _completed = true;
            Report(progress, SetupStep.Finalize, SetupStepStatus.Done, "Setup token removed");
            logger.LogInformation("Setup completed on provider {Provider}.", settings.Provider);
            return Result.Success();
        }
        finally
        {
            _installLock.Release();
        }
    }

    private static void Report(IProgress<SetupProgressUpdate>? progress, SetupStep step, SetupStepStatus status, string? message = null) =>
        progress?.Report(new SetupProgressUpdate(step, status, message));

    private static string SafeMessage(string message)
    {
        var singleLine = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return singleLine.Length <= 400 ? singleLine : singleLine[..400] + "…";
    }

    private static string? PrepareSqliteDirectory(string connectionString)
    {
        try
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            if (builder.TryGetValue("Data Source", out var value) && value is string path && path.Length > 0 && path != ":memory:" && !path.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(path));
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }

            return null;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return "The folder of the database file cannot be created: " + SafeMessage(ex.Message);
        }
    }

    private async Task<bool> CreateSchemaAsync(DatabaseSettings settings, CancellationToken cancellationToken)
    {
        await using var db = new ServoraDbContext(optionsProvider.Build(settings)) { ActorName = "System Setup" };
        if (!db.Database.GetMigrations().Any())
        {
            throw new InvalidOperationException("This Servora build contains no database migrations, so the schema cannot be created. Use an official build.");
        }

        var applied = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
        if (applied.Count == 0)
        {
            var creator = db.GetService<IRelationalDatabaseCreator>();
            if (await creator.ExistsAsync(cancellationToken) && await creator.HasTablesAsync(cancellationToken))
            {
                // Tables without migration history: a foreign database or a pre-release Servora database. Neither can
                // be migrated safely, so refuse instead of failing halfway through the schema creation.
                throw new InvalidOperationException(
                    "The database already contains tables but no Servora migration history. Use an empty database " +
                    "(see docs/operations/upgrade.md for databases created by pre-release builds).");
            }
        }

        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        await db.Database.MigrateAsync(cancellationToken);
        return pending.Count > 0;
    }

    private async Task<Result> ApplyOrganizationAsync(DatabaseSettings settings, SetupOrganizationInput organization, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ActorContext>().Set(ActorInfo.SystemNamed("System Setup"));
        var settingsService = scope.ServiceProvider.GetRequiredService<ISystemSettingsService>();
        var values = new Dictionary<string, string?>(organization.ToSettings(), StringComparer.Ordinal)
        {
            [SystemSettingKeys.FromName] = organization.ProductName,
        };
        var result = await settingsService.SetManyAsync(values, cancellationToken);
        if (!result.Succeeded)
        {
            return result;
        }

        await scope.ServiceProvider.GetRequiredService<IAuditService>().WriteAsync(new AuditEntry(SystemSettingsService.AuditCategory, "setup.completed")
        {
            TargetType = "database",
            TargetName = settings.Provider,
            Message = $"Setup completed on {settings.Provider}.",
            Details = new { provider = settings.Provider },
        }, cancellationToken);
        return Result.Success();
    }
}
