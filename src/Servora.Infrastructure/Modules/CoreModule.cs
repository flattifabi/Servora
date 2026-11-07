using Microsoft.Extensions.DependencyInjection;
using Servora.Application.Activity;
using Servora.Application.Approvals;
using Servora.Application.Automation;
using Servora.Application.Common;
using Servora.Application.Directory;
using Servora.Application.Events;
using Servora.Application.Identity;
using Servora.Application.Integration;
using Servora.Application.Notifications;
using Servora.Application.Observability;
using Servora.Application.Platform;
using Servora.Application.Security;
using Servora.Application.Sla;
using Servora.Application.Tables;
using Servora.Application.Conditions;
using Servora.Infrastructure.Activity;
using Servora.Infrastructure.Common;
using Servora.Infrastructure.Events;
using Servora.Infrastructure.Identity;
using Servora.Infrastructure.Observability;
using Servora.Infrastructure.Persistence;
using Servora.Infrastructure.Placeholders;
using Servora.Infrastructure.Platform;
using Servora.Infrastructure.Security;

namespace Servora.Infrastructure.Modules;

/// <summary>Registers the platform kernel: persistence, actor resolution, security primitives, events and shared services.</summary>
public static class CoreModule
{
    /// <summary>Registers the core services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCoreModule(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.AddSingleton<IClock, SystemClock>();

        // Persistence
        services.AddSingleton<DatabaseSettingsStore>();
        services.AddSingleton<DatabaseProviderRegistry>();
        services.AddSingleton<DbContextOptionsProvider>();
        services.AddScoped<AppDbContextFactory>();
        services.AddScoped<IAppDbContextFactory>(sp => sp.GetRequiredService<AppDbContextFactory>());
        services.AddSingleton<DatabaseInitializer>();

        // Actor / security
        services.AddScoped<ActorContext>();
        services.AddScoped<CircuitPrincipalHolder>();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddSingleton<IPasswordHasherService, PasswordHasherService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<ISecretStore, DataProtectionSecretStore>();
        services.AddScoped<ISignInService, SignInService>();

        // Record events
        services.AddSingleton<RecordEventQueue>();
        services.AddSingleton<IRecordEventPublisher>(sp => sp.GetRequiredService<RecordEventQueue>());
        services.AddHostedService<RecordEventProcessor>();

        // Shared services
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<INumberSequenceService, NumberSequenceService>();
        services.AddScoped<IActivityService, ActivityService>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddScoped<IDirectoryLookupService, DirectoryLookupService>();

        // Placeholders for cross-module contracts. Owning modules register the real implementations afterwards
        // (the last registration wins), so every module can also run in isolation.
        services.AddSingleton<ITableRegistry, ReflectionTableRegistry>();
        services.AddScoped<IRecordService, UnavailableRecordService>();
        services.AddSingleton<IConditionEvaluator, PassThroughConditionEvaluator>();
        services.AddScoped<IEmailSender, NullEmailSender>();
        services.AddScoped<IApprovalService, UnavailableApprovalService>();
        services.AddScoped<IFlowExecutionService, UnavailableFlowExecutionService>();
        services.AddScoped<IDataSourceExecutor, UnavailableDataSourceExecutor>();
        services.AddScoped<ISlaService, EmptySlaService>();

        // Seed data
        services.AddScoped<IDataSeeder, IdentitySeeder>();
        return services;
    }
}
