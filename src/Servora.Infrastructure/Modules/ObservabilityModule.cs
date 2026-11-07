using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Servora.Application.Analytics;
using Servora.Application.Common;
using Servora.Application.Observability;
using Servora.Infrastructure.Analytics;
using Servora.Infrastructure.Observability;
using Servora.Infrastructure.Observability.Health;
using Servora.Infrastructure.Observability.Metrics;

namespace Servora.Infrastructure.Modules;

/// <summary>Service registrations of the Observability &amp; Analytics (T-09): logging, debug switch, audit viewer, health, dashboards, reports module.</summary>
/// <remarks>
/// Owned by the module's task. Only the owning task edits this file. The logger provider, its sinks and the runtime log
/// level switch are registered on the logging builder by <c>ObservabilityWebModule</c> (host level).
/// </remarks>
public static class ObservabilityModule
{
    /// <summary>Health check tag for checks that are part of the readiness probe.</summary>
    public const string ReadyTag = "ready";

    /// <summary>Registers the module services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddObservabilityModule(this IServiceCollection services)
    {
        // Observability
        services.AddSingleton<IHeartbeatRegistry, HeartbeatRegistry>();
        services.AddSingleton<IRequestMetrics, RequestMetricsCollector>();
        services.AddSingleton<IProcessMetrics, ProcessMetricsSampler>();
        services.AddScoped<ILoggingAdminService, LoggingAdminService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<ISystemHealthService, SystemHealthService>();
        services.AddHostedService<ObservabilityMaintenanceService>();

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", HealthStatus.Unhealthy, [ReadyTag])
            .AddCheck<DiskSpaceHealthCheck>("disk", HealthStatus.Unhealthy, [ReadyTag])
            .AddCheck<EventQueueHealthCheck>("event-queue", HealthStatus.Unhealthy, [ReadyTag])
            .AddCheck<BackgroundServicesHealthCheck>("background-services", HealthStatus.Unhealthy, [ReadyTag])
            .AddCheck<ProcessHealthCheck>("process", HealthStatus.Degraded);

        // Analytics
        services.AddScoped<ReportEngine>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IDataSeeder, AnalyticsSeeder>();
        return services;
    }
}
