using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Servora.Application.Common;
using Servora.Application.Configuration;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Domain.Observability;
using Servora.Infrastructure.Persistence;

namespace Servora.Infrastructure.Configuration;

/// <summary>Default <see cref="IDatabaseAdminService"/>. Uses only provider-neutral EF Core / ADO.NET APIs.</summary>
/// <param name="settingsStore">The database settings store.</param>
/// <param name="providers">The provider registry.</param>
/// <param name="dbFactory">The concrete context factory (needs the full EF API).</param>
/// <param name="paths">The application paths.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="audit">The audit service.</param>
/// <param name="logger">The logger.</param>
public sealed class DatabaseAdminService(
    DatabaseSettingsStore settingsStore,
    DatabaseProviderRegistry providers,
    AppDbContextFactory dbFactory,
    IAppPaths paths,
    ICurrentUser currentUser,
    IAuditService audit,
    ILogger<DatabaseAdminService> logger) : IDatabaseAdminService
{
    private static readonly TimeSpan s_testTimeout = TimeSpan.FromSeconds(15);

    /// <inheritdoc />
    public async Task<Result<DatabaseInfo>> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Configuration.ManageDatabase, cancellationToken))
        {
            return Result<DatabaseInfo>.Forbidden();
        }

        var settings = settingsStore.Current;
        if (settings is null)
        {
            return Result<DatabaseInfo>.NotFound("No database is configured.");
        }

        var provider = providers.Get(settings.Provider);
        var info = new DatabaseInfo
        {
            ProviderKey = provider.Key,
            ProviderName = provider.DisplayName,
            RedactedConnectionString = provider.Redact(settings.ConnectionString),
            ConfigurationSource = File.Exists(Path.Combine(paths.DataDirectory, "database.json"))
                ? "Encrypted database.json in the data directory"
                : "Application configuration (Servora:Database)",
        };

        try
        {
            await using var db = dbFactory.Create();
            var connection = db.Database.GetDbConnection();
            await db.Database.OpenConnectionAsync(cancellationToken);
            try
            {
                var migrations = db.Database.GetMigrations().ToList();
                var applied = migrations.Count > 0 ? (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).Count() : 0;
                info = info with
                {
                    DatabaseName = connection.Database,
                    DataSource = connection.DataSource,
                    ServerVersion = connection.ServerVersion,
                    UsesMigrations = migrations.Count > 0,
                    AppliedMigrations = applied,
                    PendingMigrations = Math.Max(0, migrations.Count - applied),
                    SizeBytes = provider.Key == DatabaseProviderKeys.Sqlite ? GetSqliteSize(connection.DataSource) : null,
                };
            }
            finally
            {
                await db.Database.CloseConnectionAsync();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Database information could not be read.");
            info = info with { Error = "The database could not be inspected: " + SingleLine(ex.Message) };
        }

        return Result<DatabaseInfo>.Success(info);
    }

    /// <inheritdoc />
    public async Task<Result<TimeSpan>> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Configuration.ManageDatabase, cancellationToken))
        {
            return Result<TimeSpan>.Forbidden();
        }

        var settings = settingsStore.Current;
        if (settings is null)
        {
            return Result<TimeSpan>.NotFound("No database is configured.");
        }

        var provider = providers.Get(settings.Provider);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(s_testTimeout);
        var stopwatch = Stopwatch.StartNew();
        string? error;
        try
        {
            error = await provider.TestConnectionAsync(settings.ConnectionString, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            error = $"The connection attempt timed out after {s_testTimeout.TotalSeconds:0} seconds.";
        }

        stopwatch.Stop();
        await audit.WriteAsync(new AuditEntry(SystemSettingsService.AuditCategory, "database.test", error is null ? AuditOutcome.Success : AuditOutcome.Failure)
        {
            TargetType = "database",
            TargetName = provider.Key,
            Message = error is null ? $"Connection test succeeded ({stopwatch.ElapsedMilliseconds} ms)." : "Connection test failed.",
        }, cancellationToken);

        return error is null ? Result<TimeSpan>.Success(stopwatch.Elapsed) : Result<TimeSpan>.Failure(SingleLine(error));
    }

    private static long? GetSqliteSize(string? dataSource)
    {
        if (string.IsNullOrEmpty(dataSource) || !File.Exists(dataSource))
        {
            return null;
        }

        var size = new FileInfo(dataSource).Length;
        var wal = dataSource + "-wal";
        return File.Exists(wal) ? size + new FileInfo(wal).Length : size;
    }

    private static string SingleLine(string message)
    {
        var value = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return value.Length <= 400 ? value : value[..400] + "…";
    }
}
