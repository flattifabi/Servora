using Servora.Domain.Common;

namespace Servora.Domain.Integration;

/// <summary>Secret-free log entry of one data source endpoint execution (parameter values are never stored).</summary>
public class DataSourceExecutionLog : Entity
{
    /// <summary>Gets or sets the data source.</summary>
    public Guid DataSourceId { get; set; }

    /// <summary>Gets or sets the data source name at execution time.</summary>
    public string DataSourceName { get; set; } = string.Empty;

    /// <summary>Gets or sets the endpoint key.</summary>
    public string EndpointKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the HTTP method.</summary>
    public string Method { get; set; } = "GET";

    /// <summary>Gets or sets the target host.</summary>
    public string? Host { get; set; }

    /// <summary>Gets or sets the UTC start time.</summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>Gets or sets the duration in milliseconds.</summary>
    public long DurationMs { get; set; }

    /// <summary>Gets or sets the HTTP status code (0 when no response was received).</summary>
    public int StatusCode { get; set; }

    /// <summary>Gets or sets a value indicating whether the call succeeded (2xx).</summary>
    public bool Succeeded { get; set; }

    /// <summary>Gets or sets the response size in bytes that was read.</summary>
    public long ResponseBytes { get; set; }

    /// <summary>Gets or sets a value indicating whether the response was truncated.</summary>
    public bool Truncated { get; set; }

    /// <summary>Gets or sets the number of attempts.</summary>
    public int Attempts { get; set; } = 1;

    /// <summary>Gets or sets the user-safe error message.</summary>
    public string? Error { get; set; }

    /// <summary>Gets or sets the display name of the actor.</summary>
    public string? ActorName { get; set; }

    /// <summary>Gets or sets a value indicating whether the call was a test from the administration UI.</summary>
    public bool IsTest { get; set; }
}
