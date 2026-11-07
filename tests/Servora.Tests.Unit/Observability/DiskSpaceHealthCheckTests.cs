using Microsoft.Extensions.Diagnostics.HealthChecks;
using Servora.Infrastructure.Observability.Health;

namespace Servora.Tests.Unit.Observability;

/// <summary>Tests for the disk space thresholds of the health check.</summary>
public sealed class DiskSpaceHealthCheckTests
{
    private const long Gigabyte = 1024L * 1024 * 1024;

    [Theory]
    [InlineData(0.25, 100, HealthStatus.Unhealthy)]
    [InlineData(1.5, 100, HealthStatus.Degraded)]
    [InlineData(5, 100, HealthStatus.Degraded)]
    [InlineData(15, 100, HealthStatus.Healthy)]
    [InlineData(189, 1900, HealthStatus.Healthy)]
    [InlineData(19, 1900, HealthStatus.Degraded)]
    public void Classify_UsesAbsoluteAndRelativeThresholds(double freeGb, double totalGb, HealthStatus expected) =>
        Assert.Equal(expected, DiskSpaceHealthCheck.Classify((long)(freeGb * Gigabyte), (long)(totalGb * Gigabyte)));
}
