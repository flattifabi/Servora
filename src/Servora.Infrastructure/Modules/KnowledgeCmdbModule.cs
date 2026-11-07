using Microsoft.Extensions.DependencyInjection;
using Servora.Application.Assets;
using Servora.Application.Cmdb;
using Servora.Application.Common;
using Servora.Application.Knowledge;
using Servora.Infrastructure.Assets;
using Servora.Infrastructure.Cmdb;
using Servora.Infrastructure.Knowledge;

namespace Servora.Infrastructure.Modules;

/// <summary>Service registrations of the Knowledge Management, CMDB and Asset Management (T-06) module.</summary>
/// <remarks>Owned by the module's task. Only the owning task edits this file.</remarks>
public static class KnowledgeCmdbModule
{
    /// <summary>Registers the module services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddKnowledgeCmdbModule(this IServiceCollection services)
    {
        // Knowledge
        services.AddScoped<IKnowledgeService, KnowledgeService>();
        services.AddScoped<IKnowledgeSearchService, KnowledgeSearchService>();

        // CMDB
        services.AddScoped<ICmdbService, CmdbService>();
        services.AddScoped<ICiClassService, CiClassService>();

        // Assets
        services.AddScoped<IAssetService, AssetService>();

        // Seed data (CI classes and service map first, then assets and articles that reference CIs).
        services.AddScoped<IDataSeeder, CmdbSeeder>();
        services.AddScoped<IDataSeeder, AssetSeeder>();
        services.AddScoped<IDataSeeder, KnowledgeSeeder>();
        return services;
    }
}
