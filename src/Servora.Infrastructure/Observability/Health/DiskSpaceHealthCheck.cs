using System.Globalization;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Servora.Application.Common;

namespace Servora.Infrastructure.Observability.Health;

/// <summary>
/// Checks the free space of the drive holding the data directory (database, logs, attachments). Unhealthy below 500 MB,
/// degraded below 2 GB, or below 10 % free while less than 20 GB are left (large volumes with plenty of absolute free
/// space are not flagged just because of the percentage).
/// </summary>
/// <param name="paths">The application paths.</param>
public sealed class DiskSpaceHealthCheck(IAppPaths paths) : IHealthCheck
{
    private const long Gigabyte = 1024L * 1024 * 1024;

    /// <summary>Classifies free disk space.</summary>
    /// <param name="freeBytes">The available free bytes.</param>
    /// <param name="totalBytes">The total size in bytes.</param>
    /// <returns>The health status.</returns>
    public static HealthStatus Classify(long freeBytes, long totalBytes)
    {
        var percent = totalBytes <= 0 ? 0 : freeBytes * 100.0 / totalBytes;
        if (freeBytes < Gigabyte / 2)
        {
            return HealthStatus.Unhealthy;
        }

        return freeBytes < 2 * Gigabyte || (percent < 10 && freeBytes < 20 * Gigabyte) ? HealthStatus.Degraded : HealthStatus.Healthy;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(paths.DataDirectory));
            if (string.IsNullOrEmpty(root))
            {
                return Task.FromResult(HealthCheckResult.Degraded("The drive of the data directory could not be determined."));
            }

            var drive = new DriveInfo(root);
            var free = drive.AvailableFreeSpace;
            var total = drive.TotalSize;
            var percent = total == 0 ? 0 : free * 100.0 / total;
            var data = new Dictionary<string, object>
            {
                ["drive"] = drive.Name,
                ["freeBytes"] = free.ToString(CultureInfo.InvariantCulture),
                ["totalBytes"] = total.ToString(CultureInfo.InvariantCulture),
                ["freePercent"] = percent.ToString("0.0", CultureInfo.InvariantCulture),
            };
            var text = string.Create(CultureInfo.InvariantCulture, $"{free / (double)Gigabyte:0.0} GB free of {total / (double)Gigabyte:0.0} GB ({percent:0.#} %).");
            return Task.FromResult(Classify(free, total) switch
            {
                HealthStatus.Unhealthy => HealthCheckResult.Unhealthy("Disk almost full: " + text, data: data),
                HealthStatus.Degraded => HealthCheckResult.Degraded("Disk space low: " + text, data: data),
                _ => HealthCheckResult.Healthy(text, data),
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return Task.FromResult(HealthCheckResult.Degraded("Disk space could not be determined.", data: new Dictionary<string, object> { ["error"] = ex.GetType().Name }));
        }
    }
}
