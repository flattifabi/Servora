using Servora.Domain.Integration;

namespace Servora.Application.Integration.DataSources;

/// <summary>A row of the data source list.</summary>
/// <param name="Id">The data source.</param>
/// <param name="Name">The name.</param>
/// <param name="Description">The description.</param>
/// <param name="BaseUrl">The base URL.</param>
/// <param name="AuthType">The authentication type.</param>
/// <param name="IsEnabled">Whether the source is enabled.</param>
/// <param name="AllowHttp">Whether plain HTTP is allowed.</param>
public sealed record DataSourceListItem(Guid Id, string Name, string? Description, string BaseUrl, DataSourceAuthType AuthType, bool IsEnabled, bool AllowHttp)
{
    /// <summary>Gets the number of endpoints.</summary>
    public int EndpointCount { get; init; }

    /// <summary>Gets the number of calls in the last 24 hours.</summary>
    public int Calls24h { get; init; }

    /// <summary>Gets the number of failed calls in the last 24 hours.</summary>
    public int Errors24h { get; init; }

    /// <summary>Gets the time of the latest call.</summary>
    public DateTimeOffset? LastCallAt { get; init; }
}
