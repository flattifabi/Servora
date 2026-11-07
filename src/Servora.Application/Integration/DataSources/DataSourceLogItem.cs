namespace Servora.Application.Integration.DataSources;

/// <summary>An execution log entry (no parameter values, no credentials).</summary>
/// <param name="Id">The entry.</param>
/// <param name="DataSourceId">The data source.</param>
/// <param name="DataSourceName">The data source name.</param>
/// <param name="EndpointKey">The endpoint key.</param>
/// <param name="Method">The HTTP method.</param>
/// <param name="Host">The target host.</param>
/// <param name="StartedAt">The start time.</param>
/// <param name="DurationMs">The duration.</param>
/// <param name="StatusCode">The HTTP status (0 = no response).</param>
/// <param name="Succeeded">Whether the call returned 2xx.</param>
/// <param name="ResponseBytes">The bytes read.</param>
/// <param name="Truncated">Whether the response was truncated.</param>
/// <param name="Attempts">The attempts.</param>
/// <param name="Error">The error.</param>
/// <param name="ActorName">The actor.</param>
/// <param name="IsTest">Whether it was a test call.</param>
public sealed record DataSourceLogItem(
    Guid Id,
    Guid DataSourceId,
    string DataSourceName,
    string EndpointKey,
    string Method,
    string? Host,
    DateTimeOffset StartedAt,
    long DurationMs,
    int StatusCode,
    bool Succeeded,
    long ResponseBytes,
    bool Truncated,
    int Attempts,
    string? Error,
    string? ActorName,
    bool IsTest);
