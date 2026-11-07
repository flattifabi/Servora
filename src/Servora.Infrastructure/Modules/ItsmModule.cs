using Microsoft.Extensions.DependencyInjection;
using Servora.Application.Common;
using Servora.Application.Events;
using Servora.Application.Itsm;
using Servora.Infrastructure.Itsm;

namespace Servora.Infrastructure.Modules;

/// <summary>Service registrations of the Incident &amp; Problem Management (T-04) module.</summary>
/// <remarks>Owned by the module's task. Only the owning task edits this file.</remarks>
public static class ItsmModule
{
    /// <summary>Registers the module services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddItsmModule(this IServiceCollection services)
    {
        services.AddScoped<IIncidentService, IncidentService>();
        services.AddScoped<IProblemService, ProblemService>();
        services.AddScoped<IMyWorkService, MyWorkService>();
        services.AddScoped<IRecordSavingHook, ItsmSavingHook>();
        services.AddScoped<IDataSeeder, ItsmDataSeeder>();
        return services;
    }
}
