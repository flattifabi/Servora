using Microsoft.Extensions.DependencyInjection;
using Servora.Application.Automation;
using Servora.Application.Automation.Actions;
using Servora.Application.Automation.Engine;
using Servora.Application.Automation.Scheduling;
using Servora.Application.Automation.Services;
using Servora.Application.Common;
using Servora.Application.Events;
using Servora.Application.Integration;
using Servora.Application.Integration.DataSources;
using Servora.Application.Integration.Imports;
using Servora.Infrastructure.Automation;
using Servora.Infrastructure.Integration;

namespace Servora.Infrastructure.Modules;

/// <summary>Service registrations of the Automation &amp; Integration (T-08): flows, scheduler, REST data sources, import sets module.</summary>
/// <remarks>Owned by the module's task. Only the owning task edits this file.</remarks>
public static class AutomationModule
{
    /// <summary>Registers the module services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAutomationModule(this IServiceCollection services)
    {
        services.AddOptions<FlowEngineOptions>().BindConfiguration(FlowEngineOptions.SectionName);

        // Flow actions (extension point: register additional IFlowAction implementations as scoped services).
        services.AddScoped<IFlowAction, CreateRecordAction>();
        services.AddScoped<IFlowAction, UpdateRecordAction>();
        services.AddScoped<IFlowAction, LookupRecordsAction>();
        services.AddScoped<IFlowAction, ConditionAction>();
        services.AddScoped<IFlowAction, WaitAction>();
        services.AddScoped<IFlowAction, ApprovalAction>();
        services.AddScoped<IFlowAction, SendEmailAction>();
        services.AddScoped<IFlowAction, CallDataSourceAction>();
        services.AddScoped<IFlowAction, AddWorkNoteAction>();
        services.AddScoped<IFlowAction, LogAction>();
        services.AddScoped<IFlowAction, SetVariableAction>();
        services.AddScoped<IFlowAction, SubflowAction>();
        services.AddScoped<FlowActionRegistry>();

        // Engine
        services.AddSingleton<FlowRunQueue>();
        services.AddSingleton<IFlowRunQueue>(sp => sp.GetRequiredService<FlowRunQueue>());
        services.AddScoped<IFlowRunStarter, FlowRunStarter>();
        services.AddScoped<FlowRunner>();
        services.AddScoped<IRecordEventHandler, RecordTriggerHandler>();
        services.AddHostedService<FlowEngineWorker>();

        // Flow services (the contract implementation replaces the kernel placeholder).
        services.AddScoped<IFlowExecutionService, FlowExecutionService>();
        services.AddScoped<IFlowService, FlowService>();
        services.AddScoped<IFlowRunService, FlowRunService>();
        services.AddScoped<FlowWebhookService>();

        // Scheduler
        services.AddScoped<IScheduleService, ScheduleService>();
        services.AddScoped<ScheduleProcessor>();
        services.AddHostedService<SchedulerWorker>();

        // REST data sources with SSRF protection (connect-time IP validation in the named client's handler).
        services.AddSingleton<IDnsResolver, SystemDnsResolver>();
        services.AddSingleton<INetworkAllowListProvider, NetworkAllowListProvider>();
        services.AddSingleton<SsrfGuard>();
        services.AddSingleton<DataSourceRateLimiter>();
        services.AddHttpClient(DataSourceHttp.ClientName, client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(sp => DataSourceHttp.CreateHandler(sp.GetRequiredService<SsrfGuard>()))
            .SetHandlerLifetime(TimeSpan.FromMinutes(5))
            .RemoveAllLoggers();
        services.AddScoped<IDataSourceInvoker, HttpDataSourceInvoker>();
        services.AddScoped<DataSourceExecutor>();
        services.AddScoped<IDataSourceExecutor>(sp => sp.GetRequiredService<DataSourceExecutor>());
        services.AddScoped<IDataSourceAdminService, DataSourceAdminService>();

        // Import sets
        services.AddScoped<ImportProcessor>();
        services.AddScoped<IImportSetService, ImportSetService>();

        services.AddScoped<IDataSeeder, AutomationSeeder>();
        return services;
    }
}
