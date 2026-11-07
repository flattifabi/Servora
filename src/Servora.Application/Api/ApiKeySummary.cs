namespace Servora.Application.Api;

/// <summary>List row of an API key. Never contains secret material.</summary>
/// <param name="Id">The identifier.</param>
/// <param name="Name">The name.</param>
/// <param name="KeyPreview">The masked key (<c>srv_&lt;key id&gt;_••••</c>).</param>
/// <param name="OwnerName">The owner display name or service identity.</param>
/// <param name="IsServiceIdentity">Whether the key belongs to a service identity instead of a user.</param>
/// <param name="ScopeCount">The number of scopes.</param>
/// <param name="Status">The effective status.</param>
/// <param name="CreatedAt">The UTC creation time.</param>
/// <param name="ExpiresAt">The UTC expiry time.</param>
/// <param name="LastUsedAt">The UTC time of the last successful use.</param>
/// <param name="UsageCount">The total number of authenticated requests.</param>
/// <param name="RateLimitPerMinute">The rate limit.</param>
public sealed record ApiKeySummary(
    Guid Id,
    string Name,
    string KeyPreview,
    string OwnerName,
    bool IsServiceIdentity,
    int ScopeCount,
    ApiKeyStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt,
    long UsageCount,
    int RateLimitPerMinute);
