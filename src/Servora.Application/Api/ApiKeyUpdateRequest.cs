namespace Servora.Application.Api;

/// <summary>Input for changing the settings of an existing API key (the secret is changed with rotation only).</summary>
public sealed record ApiKeyUpdateRequest
{
    /// <summary>Gets the display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the scopes.</summary>
    public IReadOnlyList<string> Scopes { get; init; } = [];

    /// <summary>Gets the UTC expiry time.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Gets the allowed IP ranges.</summary>
    public string? AllowedIpRanges { get; init; }

    /// <summary>Gets the rate limit per minute.</summary>
    public int RateLimitPerMinute { get; init; } = ApiKeyLimits.DefaultRateLimitPerMinute;
}
