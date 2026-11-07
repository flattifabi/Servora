using Servora.Domain.Common;

namespace Servora.Domain.Api;

/// <summary>
/// An API key used by integrations to call the REST API and the MCP server. Only a SHA-256 hash of the secret is
/// stored; the plain token (<c>srv_&lt;key id&gt;_&lt;secret&gt;</c>) is shown exactly once when the key is created or rotated.
/// </summary>
public class ApiKey : AuditableEntity
{
    /// <summary>Gets or sets the display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets an optional description (purpose, system, contact).</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the public key identifier embedded in the token (lower-case alphanumeric, unique).</summary>
    public string KeyId { get; set; } = string.Empty;

    /// <summary>Gets or sets the lower-case hex SHA-256 hash of the secret part of the token.</summary>
    [NotExposed]
    public string SecretHash { get; set; } = string.Empty;

    /// <summary>Gets or sets the user the key acts for. <see langword="null"/> for a service identity.</summary>
    [Reference(TableNames.User)]
    public Guid? OwnerUserId { get; set; }

    /// <summary>Gets or sets the service identity name when the key is not owned by a user.</summary>
    public string? ServiceIdentity { get; set; }

    /// <summary>Gets or sets the granted permission scopes, separated by spaces.</summary>
    public string Scopes { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC expiry time. <see langword="null"/> means the key never expires.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>Gets or sets the allowed client IP addresses / CIDR ranges, separated by commas. Empty allows any address.</summary>
    public string? AllowedIpRanges { get; set; }

    /// <summary>Gets or sets the maximum number of requests per minute.</summary>
    public int RateLimitPerMinute { get; set; } = 120;

    /// <summary>Gets or sets the UTC time the key was revoked.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>Gets or sets the display name of the actor that revoked the key.</summary>
    public string? RevokedBy { get; set; }

    /// <summary>Gets or sets the revocation reason.</summary>
    public string? RevocationReason { get; set; }

    /// <summary>Gets or sets the UTC time of the last secret rotation.</summary>
    public DateTimeOffset? RotatedAt { get; set; }

    /// <summary>Gets or sets the UTC time of the last successful authentication.</summary>
    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>Gets or sets the client IP address of the last successful authentication.</summary>
    public string? LastUsedIp { get; set; }

    /// <summary>Gets or sets the total number of successfully authenticated requests.</summary>
    public long UsageCount { get; set; }
}
