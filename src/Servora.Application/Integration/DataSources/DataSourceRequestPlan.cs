namespace Servora.Application.Integration.DataSources;

/// <summary>A validated outgoing request built from an endpoint definition and parameter values.</summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="Uri">The absolute request URI (same origin and base path as the data source).</param>
/// <param name="Body">The JSON body, if any.</param>
public sealed record DataSourceRequestPlan(string Method, Uri Uri, string? Body);
