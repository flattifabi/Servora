namespace Servora.Application.Integration;

/// <summary>An approved endpoint of a data source.</summary>
/// <param name="Key">The endpoint key used for execution.</param>
/// <param name="Name">The display name.</param>
/// <param name="Method">The HTTP method.</param>
/// <param name="Description">The description.</param>
/// <param name="Parameters">The names of the accepted parameters.</param>
public sealed record DataSourceEndpointSummary(string Key, string Name, string Method, string? Description, IReadOnlyList<string> Parameters);
