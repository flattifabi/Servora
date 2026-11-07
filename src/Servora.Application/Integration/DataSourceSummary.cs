namespace Servora.Application.Integration;

/// <summary>A data source accessible to the current actor.</summary>
/// <param name="Id">The data source identifier.</param>
/// <param name="Name">The name.</param>
/// <param name="Description">The description.</param>
/// <param name="Endpoints">The approved endpoints.</param>
public sealed record DataSourceSummary(Guid Id, string Name, string? Description, IReadOnlyList<DataSourceEndpointSummary> Endpoints);
