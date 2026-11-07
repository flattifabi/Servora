using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Servora.Application.Common;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Infrastructure.Persistence;

namespace Servora.Infrastructure.Observability.Health;

/// <summary>Default <see cref="ISystemHealthService"/> built on ASP.NET Core health checks.</summary>
/// <param name="healthChecks">The health check service.</param>
/// <param name="currentUser">The current actor.</param>
/// <param name="requestMetrics">The request metrics.</param>
/// <param name="processMetrics">The process metrics.</param>
/// <param name="heartbeats">The heartbeat registry.</param>
/// <param name="paths">The application paths.</param>
/// <param name="databaseSettings">The database settings.</param>
/// <param name="environment">The host environment.</param>
public sealed class SystemHealthService(
    HealthCheckService healthChecks,
    ICurrentUser currentUser,
    IRequestMetrics requestMetrics,
    IProcessMetrics processMetrics,
    IHeartbeatRegistry heartbeats,
    IAppPaths paths,
    DatabaseSettingsStore databaseSettings,
    IHostEnvironment environment) : ISystemHealthService
{
    /// <summary>Gets the product version (informational version without build metadata).</summary>
    public static string ProductVersion { get; } =
        (Assembly.GetEntryAssembly() ?? typeof(SystemHealthService).Assembly).GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? typeof(SystemHealthService).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    /// <summary>Converts an ASP.NET Core health report into the application DTO.</summary>
    /// <param name="report">The report.</param>
    /// <returns>The entries.</returns>
    public static IReadOnlyList<HealthCheckEntry> ToEntries(HealthReport report) =>
        report.Entries
            .Select(e => new HealthCheckEntry(
                e.Key,
                e.Value.Status.ToString(),
                e.Value.Description,
                Math.Round(e.Value.Duration.TotalMilliseconds, 1),
                e.Value.Data.ToDictionary(d => d.Key, d => Convert.ToString(d.Value, CultureInfo.InvariantCulture) ?? string.Empty)))
            .ToList();

    /// <inheritdoc />
    public async Task<Result<SystemHealthReport>> GetReportAsync(TimeSpan? metricsWindow = null, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Observability.ReadHealth, cancellationToken))
        {
            return Result<SystemHealthReport>.Forbidden();
        }

        var report = await healthChecks.CheckHealthAsync(cancellationToken);
        var sample = processMetrics.Current;
        var process = new ProcessInfo(
            ProductVersion,
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.OSDescription,
            Environment.MachineName,
            Environment.ProcessorCount,
            processMetrics.StartedAt,
            DateTimeOffset.UtcNow - processMetrics.StartedAt,
            sample,
            GC.CollectionCount(0),
            GC.CollectionCount(1),
            GC.CollectionCount(2),
            paths.DataDirectory,
            databaseSettings.Current?.Provider,
            environment.EnvironmentName);

        return Result<SystemHealthReport>.Success(new SystemHealthReport(
            report.Status.ToString(),
            DateTimeOffset.UtcNow,
            Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            ToEntries(report),
            process,
            processMetrics.History,
            requestMetrics.GetSnapshot(metricsWindow ?? TimeSpan.FromHours(1)),
            heartbeats.GetAll()));
    }
}
