namespace Servora.Web.Observability;

/// <summary>
/// Puts the correlation id and request metrics middleware at the very beginning of the request pipeline (before the
/// middleware configured in <c>Program.cs</c>), so every request — including rejected and failed ones — is measured and
/// correlated.
/// </summary>
public sealed class ObservabilityStartupFilter : IStartupFilter
{
    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<RequestMetricsMiddleware>();
        next(app);
    };
}
