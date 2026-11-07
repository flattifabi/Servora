namespace Servora.Application.Integration.DataSources;

/// <summary>Key figures of the data source area.</summary>
/// <param name="Sources">The number of data sources.</param>
/// <param name="Enabled">The number of enabled data sources.</param>
/// <param name="Endpoints">The number of endpoints.</param>
/// <param name="Calls24h">Calls in the last 24 hours.</param>
/// <param name="ErrorRate24h">The failure rate in percent.</param>
/// <param name="AverageDurationMs">The average duration in milliseconds.</param>
/// <param name="HourlyCalls">Calls per hour for the last 24 hours.</param>
public sealed record DataSourceOverview(int Sources, int Enabled, int Endpoints, int Calls24h, double ErrorRate24h, long AverageDurationMs, IReadOnlyList<double> HourlyCalls);
