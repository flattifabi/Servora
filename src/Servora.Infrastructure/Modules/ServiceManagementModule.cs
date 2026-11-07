using Microsoft.Extensions.DependencyInjection;
using Servora.Application.Approvals;
using Servora.Application.Catalog;
using Servora.Application.Changes;
using Servora.Application.Common;
using Servora.Application.Events;
using Servora.Infrastructure.Catalog;
using Servora.Infrastructure.Changes;

namespace Servora.Infrastructure.Modules;

/// <summary>Service registrations of the Change Management, Service Catalog, Requests and Approvals (T-05) module.</summary>
/// <remarks>Owned by the module's task. Only the owning task edits this file.</remarks>
public static class ServiceManagementModule
{
    /// <summary>Registers the module services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddServiceManagementModule(this IServiceCollection services)
    {
        // Approvals engine. The IApprovalService registration replaces the kernel placeholder (last registration wins).
        services.AddScoped<ApprovalEngine>();
        services.AddScoped<IApprovalService, ApprovalService>();
        services.AddScoped<IApprovalInboxService, ApprovalInboxService>();

        // Record handlers connecting approvable tables to the engine.
        services.AddScoped<IApprovalRecordHandler, ChangeApprovalHandler>();
        services.AddScoped<IApprovalRecordHandler, RequestItemApprovalHandler>();

        // Change management.
        services.AddScoped<IChangeService, ChangeService>();
        services.AddScoped<IRecordSavingHook, ChangeSavingHook>();

        // Service catalog and requests.
        services.AddScoped<CatalogFulfillmentEngine>();
        services.AddScoped<ICatalogAdminService, CatalogAdminService>();
        services.AddScoped<ICatalogOrderService, CatalogOrderService>();
        services.AddScoped<IRequestService, RequestService>();
        services.AddScoped<IRecordEventHandler, CatalogTaskEventHandler>();

        // Seed data (CAB group, demo changes, catalog).
        services.AddScoped<IDataSeeder, ChangeManagementSeeder>();
        services.AddScoped<IDataSeeder, CatalogSeeder>();
        return services;
    }
}
