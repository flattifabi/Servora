using Microsoft.Extensions.DependencyInjection;
using Servora.Application.Common;
using Servora.Application.Portal;
using Servora.Infrastructure.Portal;

namespace Servora.Infrastructure.Modules;

/// <summary>Service registrations of the Self-service portal (T-11) module.</summary>
/// <remarks>Owned by the module's task. Only the owning task edits this file.</remarks>
public static class PortalModule
{
    /// <summary>Registers the module services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddPortalModule(this IServiceCollection services)
    {
        services.AddScoped<IPortalTicketService, PortalTicketService>();
        services.AddScoped<IPortalConversationService, PortalConversationService>();
        services.AddScoped<IPortalKnowledgeService, PortalKnowledgeService>();
        services.AddScoped<IPortalApprovalService, PortalApprovalService>();
        services.AddScoped<IPortalAnnouncementService, PortalAnnouncementService>();
        services.AddScoped<IPortalHomeService, PortalHomeService>();
        services.AddScoped<IDataSeeder, PortalSeeder>();
        return services;
    }
}
