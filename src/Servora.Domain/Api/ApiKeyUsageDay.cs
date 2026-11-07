using Servora.Domain.Common;

namespace Servora.Domain.Api;

/// <summary>Aggregated request counters of one API key for one UTC day (usage charts).</summary>
public class ApiKeyUsageDay : Entity
{
    /// <summary>Gets or sets the API key identifier.</summary>
    public Guid ApiKeyId { get; set; }

    /// <summary>Gets or sets the UTC day (midnight, offset zero).</summary>
    public DateTimeOffset Day { get; set; }

    /// <summary>Gets or sets the number of successfully authenticated requests.</summary>
    public long RequestCount { get; set; }

    /// <summary>Gets or sets the number of rejected authentication attempts for the key.</summary>
    public long FailedCount { get; set; }
}
