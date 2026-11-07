namespace Servora.Application.Api;

/// <summary>Detail view of an API key. Never contains the secret or its hash.</summary>
public sealed record ApiKeyDetails
{
    /// <summary>Gets the identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the public key identifier.</summary>
    public required string KeyId { get; init; }

    /// <summary>Gets the masked key.</summary>
    public required string KeyPreview { get; init; }

    /// <summary>Gets the owner user identifier.</summary>
    public Guid? OwnerUserId { get; init; }

    /// <summary>Gets the owner display name or service identity.</summary>
    public required string OwnerName { get; init; }

    /// <summary>Gets the service identity name.</summary>
    public string? ServiceIdentity { get; init; }

    /// <summary>Gets the scopes.</summary>
    public required IReadOnlyList<string> Scopes { get; init; }

    /// <summary>Gets the allowed IP ranges (empty = any).</summary>
    public required IReadOnlyList<string> AllowedIpRanges { get; init; }

    /// <summary>Gets the rate limit per minute.</summary>
    public int RateLimitPerMinute { get; init; }

    /// <summary>Gets the effective status.</summary>
    public ApiKeyStatus Status { get; init; }

    /// <summary>Gets the UTC creation time.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the creator display name.</summary>
    public string? CreatedBy { get; init; }

    /// <summary>Gets the UTC expiry time.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Gets the UTC time of the last rotation.</summary>
    public DateTimeOffset? RotatedAt { get; init; }

    /// <summary>Gets the UTC revocation time.</summary>
    public DateTimeOffset? RevokedAt { get; init; }

    /// <summary>Gets who revoked the key.</summary>
    public string? RevokedBy { get; init; }

    /// <summary>Gets the revocation reason.</summary>
    public string? RevocationReason { get; init; }

    /// <summary>Gets the UTC time of the last successful use.</summary>
    public DateTimeOffset? LastUsedAt { get; init; }

    /// <summary>Gets the client IP of the last successful use.</summary>
    public string? LastUsedIp { get; init; }

    /// <summary>Gets the total number of authenticated requests.</summary>
    public long UsageCount { get; init; }
}
