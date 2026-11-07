namespace Servora.Web.Api;

/// <summary>Adds the API correlation / error middleware in front of the host pipeline (without editing Program.cs).</summary>
public sealed class ApiPipelineStartupFilter : IStartupFilter
{
    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.UseMiddleware<ApiCorrelationMiddleware>();
        next(app);
    };
}
