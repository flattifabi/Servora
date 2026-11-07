namespace Servora.Application.Api;

/// <summary>Input for creating an API key.</summary>
public sealed record ApiKeyCreateRequest
{
    /// <summary>Gets the display name (required, max 100 characters).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the optional description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the owner user; <see langword="null"/> creates a key for <see cref="ServiceIdentity"/>.</summary>
    public Guid? OwnerUserId { get; init; }

    /// <summary>Gets the service identity name (required when no owner user is set).</summary>
    public string? ServiceIdentity { get; init; }

    /// <summary>Gets the requested scopes.</summary>
    public IReadOnlyList<string> Scopes { get; init; } = [];

    /// <summary>Gets the UTC expiry time; <see langword="null"/> never expires.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Gets the allowed IP ranges (comma or newline separated).</summary>
    public string? AllowedIpRanges { get; init; }

    /// <summary>Gets the rate limit per minute.</summary>
    public int RateLimitPerMinute { get; init; } = ApiKeyLimits.DefaultRateLimitPerMinute;
}
