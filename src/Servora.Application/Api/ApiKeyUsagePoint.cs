namespace Servora.Application.Api;

/// <summary>Request counts of one UTC day.</summary>
/// <param name="Day">The UTC day.</param>
/// <param name="Requests">The authenticated requests.</param>
/// <param name="Failed">The rejected authentication attempts.</param>
public sealed record ApiKeyUsagePoint(DateTimeOffset Day, long Requests, long Failed);
