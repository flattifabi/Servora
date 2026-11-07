namespace Servora.Application.Api;

/// <summary>In-memory usage counters of one key and UTC day, waiting to be flushed to the database.</summary>
public sealed class ApiKeyUsageBucket
{
    /// <summary>Gets or sets the key identifier.</summary>
    public Guid ApiKeyId { get; init; }

    /// <summary>Gets or sets the UTC day.</summary>
    public DateTimeOffset Day { get; init; }

    /// <summary>Gets or sets the number of successful requests.</summary>
    public long Requests { get; set; }

    /// <summary>Gets or sets the number of failed attempts.</summary>
    public long Failed { get; set; }

    /// <summary>Gets or sets the UTC time of the last successful request.</summary>
    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>Gets or sets the client IP of the last successful request.</summary>
    public string? LastUsedIp { get; set; }
}
