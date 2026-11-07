using System.Net;

namespace Servora.Application.Api;

/// <summary>Cached, authentication-relevant data of an API key (contains the secret hash; never leaves the backend).</summary>
public sealed record ApiKeyIdentity
{
    /// <summary>Gets the key identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the public key id.</summary>
    public required string KeyId { get; init; }

    /// <summary>Gets the key name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the stored secret hash.</summary>
    public required string SecretHash { get; init; }

    /// <summary>Gets the owner user identifier.</summary>
    public Guid? OwnerUserId { get; init; }

    /// <summary>Gets the owner login name.</summary>
    public string? OwnerUserName { get; init; }

    /// <summary>Gets the owner display name.</summary>
    public string? OwnerDisplayName { get; init; }

    /// <summary>Gets a value indicating whether the owner account is active (always true for service identities).</summary>
    public bool OwnerActive { get; init; } = true;

    /// <summary>Gets the service identity name.</summary>
    public string? ServiceIdentity { get; init; }

    /// <summary>Gets the scopes.</summary>
    public required IReadOnlyList<string> Scopes { get; init; }

    /// <summary>Gets the UTC expiry time.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Gets the UTC revocation time.</summary>
    public DateTimeOffset? RevokedAt { get; init; }

    /// <summary>Gets the allowed networks (empty = any).</summary>
    public required IReadOnlyList<IPNetwork> AllowedNetworks { get; init; }

    /// <summary>Gets the rate limit per minute.</summary>
    public int RateLimitPerMinute { get; init; }

    /// <summary>Gets the principal name (owner login or <c>svc:&lt;identity&gt;</c>).</summary>
    public string PrincipalName => OwnerUserName ?? $"svc:{ServiceIdentity ?? KeyId}";

    /// <summary>Gets the display name used in audit fields, for example <c>Jane Doe (API key: CI pipeline)</c>.</summary>
    public string DisplayName => OwnerDisplayName is { } owner ? $"{owner} (API key: {Name})" : $"{ServiceIdentity ?? Name} (API key)";

    /// <summary>Returns a redacted representation without the secret hash.</summary>
    /// <returns>The redacted string.</returns>
    public override string ToString() => $"ApiKeyIdentity {{ Id = {Id}, KeyId = {KeyId}, Name = {Name} }}";
}
