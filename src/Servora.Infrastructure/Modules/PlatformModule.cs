using Microsoft.Extensions.DependencyInjection;
using Servora.Application.BusinessRules;
using Servora.Application.Common;
using Servora.Application.Conditions;
using Servora.Application.Events;
using Servora.Application.Notifications;
using Servora.Application.Platform;
using Servora.Application.Sla;
using Servora.Application.Tables;
using Servora.Infrastructure.Platform;
using Servora.Infrastructure.Sla;

namespace Servora.Infrastructure.Modules;

/// <summary>Service registrations of the Platform engine (T-07): table registry, generic records, conditions, custom tables, business rules, SLAs, notifications module.</summary>
/// <remarks>Owned by the module's task. Only the owning task edits this file. Registrations override the kernel placeholders (last registration wins).</remarks>
public static class PlatformModule
{
    /// <summary>Registers the module services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddPlatformModule(this IServiceCollection services)
    {
        services.AddOptions<PlatformOptions>().BindConfiguration(PlatformOptions.SectionName);

        // Conditions and tables.
        services.AddSingleton<ConditionEvaluator>();
        services.AddSingleton<IConditionEvaluator>(sp => sp.GetRequiredService<ConditionEvaluator>());
        services.AddSingleton<PlatformTableRegistry>();
        services.AddSingleton<ITableRegistry>(sp => sp.GetRequiredService<PlatformTableRegistry>());
        services.AddScoped<ITableAccessService, TableAccessService>();
        services.AddScoped<IRecordService, RecordService>();
        services.AddScoped<IRecordLookupService, RecordLookupService>();
        services.AddScoped<IRecordNumberResolver, RecordNumberResolver>();
        services.AddScoped<ITableDesignerService, TableDesignerService>();
        services.AddScoped<IRecordEventHandler, RecordFieldIndexHandler>();

        // Business rules.
        services.AddSingleton<BusinessRuleCache>();
        services.AddScoped<IBusinessRuleService, BusinessRuleService>();
        services.AddScoped<IRecordSavingHook, BusinessRuleSavingHook>();
        services.AddScoped<IRecordEventHandler, BusinessRuleEventHandler>();

        // SLAs.
        services.AddScoped<SlaEngine>();
        services.AddScoped<ISlaService, SlaService>();
        services.AddScoped<ISlaAdminService, SlaAdminService>();
        services.AddScoped<IRecordEventHandler, SlaRecordEventHandler>();
        services.AddHostedService<SlaEvaluatorService>();

        // Notifications.
        services.AddScoped<NotificationRenderer>();
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
        services.AddScoped<INotificationAdminService, NotificationAdminService>();
        services.AddScoped<INotificationInbox, NotificationInbox>();
        services.AddScoped<IRecordEventHandler, NotificationRecordEventHandler>();

        services.AddScoped<IDataSeeder, PlatformSeeder>();
        return services;
    }
}
